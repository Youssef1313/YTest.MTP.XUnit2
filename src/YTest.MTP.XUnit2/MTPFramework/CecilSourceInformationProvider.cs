using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Xunit;
using Xunit.Abstractions;

namespace YTest.MTP.XUnit2;

// Mostly taken from https://github.com/xunit/xunit/blob/4ade48a7e65aa916a20b11d38da0ec127454bf80/src/xunit.v3.runner.common/Frameworks/CecilSourceInformationProvider.cs#L10

internal sealed class CecilSourceInformationProvider : ISourceInformationProvider
{
    // 0xFEEFEE marks a "hidden" line, per https://mono-cecil.narkive.com/gFuvydFp/trouble-with-sequencepoint
    private const int SEQUENCE_POINT_HIDDEN_LINE = 0xFEEFEE;

    private static readonly HashSet<byte[]> s_publicKeyTokensToSkip = new(
    [
        [0x50, 0xce, 0xbf, 0x1c, 0xce, 0xb9, 0xd0, 0x5e],  // Mono
		[0x8d, 0x05, 0xb1, 0xbb, 0x7a, 0x6f, 0xdb, 0x6c],  // xUnit.net
	], ByteArrayComparer.Instance);

    private static readonly DefaultSymbolReaderProvider s_symbolProvider = new(throwIfNoSymbol: false);

    private readonly ConcurrentBag<ModuleDefinition> _moduleDefinitions = [];
    private readonly ConcurrentDictionary<string, TypeDefinition> _typeDefinitions = [];

    private CecilSourceInformationProvider(string assemblyFileName)
    {
        try
        {
            AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;

            AddAssembly(assemblyFileName);

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                AddAssembly(assembly);
        }
        catch { }
    }

    void AddAssembly(string assemblyFileName)
    {
        try
        {
            if (!File.Exists(assemblyFileName))
                return;

            var moduleDefinition = ModuleDefinition.ReadModule(assemblyFileName);

            // Exclude non-.NET assemblies
            if (moduleDefinition.Assembly is null)
                return;

            // Exclude things with known public keys
            var name = moduleDefinition.Assembly.Name;
            if (name.HasPublicKey && s_publicKeyTokensToSkip.Contains(name.PublicKeyToken))
                return;

            using var symbolReader = s_symbolProvider.GetSymbolReader(moduleDefinition, moduleDefinition.FileName);
            if (symbolReader is null)
                return;

            moduleDefinition.ReadSymbols(symbolReader, throwIfSymbolsAreNotMaching: false);
            if (!moduleDefinition.HasSymbols)
                return;

            _moduleDefinitions.Add(moduleDefinition);
            foreach (var typeDefinition in moduleDefinition.Types.Where(t => t.IsPublic))
                _typeDefinitions.TryAdd(typeDefinition.FullName, typeDefinition);
        }
        catch { }
    }

    void AddAssembly(Assembly assembly)
    {
        if (!assembly.IsDynamic)
            AddAssembly(assembly.Location);
    }

    /// <summary>
    /// Creates a source provider for the given test assembly.
    /// </summary>
    /// <param name="assemblyFileName">The test assembly filename</param>
    /// <remarks>
    /// This may return an instance of <see cref="NullSourceInformationProvider"/> if source information
    /// collection is turned off, or if the provided assembly does not exist on disk.
    /// </remarks>
    public static ISourceInformationProvider Create(string assemblyFileName)
    {
        if (!RunSettingsUtility.CollectSourceInformation)
            return EfficientNullSourceInformationProvider.Instance;

        if (!File.Exists(assemblyFileName))
            return EfficientNullSourceInformationProvider.Instance;

        return new CecilSourceInformationProvider(assemblyFileName);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        try
        {
            AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
        }
        catch { }

        foreach (var moduleDefinition in _moduleDefinitions.Distinct())
            moduleDefinition?.Dispose();
    }

    /// <inheritdoc/>
    public ISourceInformation GetSourceInformation(ITestCase testCase)
    {
        var testClassName = testCase.TestMethod.TestClass.Class.Name;
        var testMethodName = testCase.TestMethod.Method.Name;
        if (testClassName is null || testMethodName is null)
            return EfficientNullSourceInformationProvider.NullSourceInformation;

        try
        {
            var testClassNamePieces = testClassName.Split('+');

            if (_typeDefinitions.TryGetValue(testClassNamePieces[0], out var typeDefinition))
            {
                foreach (var nestedClassName in testClassNamePieces.Skip(1))
                {
                    typeDefinition = typeDefinition.NestedTypes.FirstOrDefault(t => t.Name == nestedClassName);
                    if (typeDefinition is null)
                        return EfficientNullSourceInformationProvider.NullSourceInformation;
                }

                var methodDefinitions = typeDefinition.GetMethods().Where(m => m.Name == testMethodName && m.IsPublic).ToList();
                if (methodDefinitions.Count == 1)
                {
                    var debugInformation = typeDefinition.Module.SymbolReader.Read(methodDefinitions[0]);
                    var sequencePoint = debugInformation.SequencePoints.FirstOrDefault(sp => sp.StartLine != SEQUENCE_POINT_HIDDEN_LINE);
                    if (sequencePoint is not null)
                        return new SourceInformation() { FileName = sequencePoint.Document.Url, LineNumber = sequencePoint.StartLine };
                }
            }
        }
        catch { }

        return EfficientNullSourceInformationProvider.NullSourceInformation;
    }

    void OnAssemblyLoad(
        object? sender,
        AssemblyLoadEventArgs args) =>
            AddAssembly(args.LoadedAssembly);

    private sealed class ByteArrayComparer : IEqualityComparer<byte[]>
    {
        public static ByteArrayComparer Instance { get; } = new();

        public bool Equals(byte[]? x, byte[]? y)
        {
            if (x is null)
                return y is null;
            if (y is null)
                return false;
            if (x.Length != y.Length)
                return false;

            return ((IStructuralEquatable)x).Equals(y, EqualityComparer<byte>.Default);
        }

        public int GetHashCode(byte[] obj) =>
            ((IStructuralEquatable)obj).GetHashCode(EqualityComparer<byte>.Default);
    }

    // xunit already has NullSourceInformationProvider but it allocates a new instance every time. This returns a cached instance.
    private sealed class EfficientNullSourceInformationProvider : LongLivedMarshalByRefObject, ISourceInformationProvider
    {
        private EfficientNullSourceInformationProvider()
        {
        }

        public static ISourceInformation NullSourceInformation { get; } = new SourceInformation();
        
        public static ISourceInformationProvider Instance { get; } = new EfficientNullSourceInformationProvider();

        public ISourceInformation GetSourceInformation(ITestCase testCase)
            => NullSourceInformation;

        public void Dispose()
        {
        }
    }

}
