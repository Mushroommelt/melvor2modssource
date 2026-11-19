using Mono.Cecil;
using Mono.Cecil.Cil;

namespace MelvorModLoaderLauncher;

/// <summary>
/// Writes a copy of GamesByMalcs.Melvor2.dll with a tiny bootstrap injected. The original is never modified.
///
/// The bootstrap is added to GodotPlugins.Game.Main (the managed entry point Godot generates for every C# game and
/// calls on startup) and runs right after Godot's script bridge is initialized. It loads the loader assembly named by
/// the <see cref="LoaderPathVariable"/> environment variable (set by the launcher) into the same AssemblyLoadContext
/// as the game and invokes MelvorModLoader.Loader.Init(), which loads the mods. Without the variable it does nothing.
/// Everything else lives in the loader and the mods, so this patch rarely needs to change between game versions.
/// </summary>
internal static class BootstrapPatcher
{
    public const string GameAssemblyFile = "GamesByMalcs.Melvor2.dll";
    public const string LoaderPathVariable = "MELVOR_MODLOADER";

    /// <summary>Bump when the injected code changes, so existing patched copies are rebuilt.</summary>
    public const int BootstrapVersion = 3;

    private const string EntryTypeName = "GodotPlugins.Game.Main";
    private const string EntryMethodName = "InitializeFromGameProject";
    private const string AnchorCallName = "LookupScriptsInAssembly";
    private const string BootstrapMethodName = "__MelvorModLoader_Load";
    private const string LoaderEntryType = "MelvorModLoader.Loader";
    private const string LoaderEntryMethod = "Init";

    // Bootstraps injected into the game's own file by earlier setups (before the launcher existed).
    private static readonly string[] s_legacyBootstrapNames = ["__MultitaskingMod_LoadMods"];

    public static bool IsPatched(string assemblyPath)
    {
        using var module = ReadModule(assemblyPath);
        return module.GetType(EntryTypeName)?.Methods.Any(m =>
            m.Name == BootstrapMethodName || s_legacyBootstrapNames.Contains(m.Name)) == true;
    }

    /// <summary>Writes a patched copy of the pristine <paramref name="originalPath"/> to <paramref name="outputPath"/>.</summary>
    public static void Patch(string originalPath, string outputPath)
    {
        // Writing resolves framework references from the game's data folder.
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(originalPath))!);
        using var module = ReadModule(originalPath, resolver);

        var entryType = module.GetType(EntryTypeName)
            ?? throw new InvalidOperationException($"Type {EntryTypeName} not found; the game's entry point changed.");
        if (entryType.Methods.Any(m => m.Name == BootstrapMethodName || s_legacyBootstrapNames.Contains(m.Name)))
        {
            throw new InvalidOperationException($"{originalPath} is already patched; expected an original game file.");
        }

        var entryMethod = entryType.Methods.Single(m => m.Name == EntryMethodName);
        var anchors = entryMethod.Body.Instructions.Where(i =>
                (i.OpCode == OpCodes.Call || i.OpCode == OpCodes.Callvirt) &&
                i.Operand is MethodReference mr && mr.Name == AnchorCallName).ToArray();
        if (anchors.Length != 1)
            throw new InvalidOperationException($"Expected exactly one call to {AnchorCallName} in {EntryMethodName}; " +
                $"found {anchors.Length}. Refusing to patch an unverified game build.");
        var anchor = anchors[0];

        var bootstrap = BuildBootstrap(module, entryType);
        entryType.Methods.Add(bootstrap);
        entryMethod.Body.GetILProcessor().InsertAfter(anchor, Instruction.Create(OpCodes.Call, bootstrap));

        // Write a new file and move it into place: never leaves a half-written file behind, and never writes through
        // an existing hard link to the original.
        var temp = outputPath + ".tmp";
        module.Write(temp);
        File.Move(temp, outputPath, overwrite: true);
    }

    // Read fully into memory so input and output may be the same file and no file handle stays open.
    private static ModuleDefinition ReadModule(string path, IAssemblyResolver? resolver = null) =>
        ModuleDefinition.ReadModule(new MemoryStream(File.ReadAllBytes(path)), new ReaderParameters { AssemblyResolver = resolver });

    // static void __MelvorModLoader_Load()
    // {
    //     try
    //     {
    //         var gameAssembly = typeof(Main).Assembly;
    //         var path = Environment.GetEnvironmentVariable("MELVOR_MODLOADER");
    //         if (File.Exists(path))
    //             AssemblyLoadContext.GetLoadContext(gameAssembly)
    //                 .LoadFromAssemblyPath(path)
    //                 .GetType("MelvorModLoader.Loader", true)
    //                 .GetMethod("Init")
    //                 .Invoke(null, null);
    //     }
    //     catch (Exception e) { Console.Error.WriteLine(e); }
    // }
    private static MethodDefinition BuildBootstrap(ModuleDefinition module, TypeDefinition entryType)
    {
        var refs = new FrameworkReferences(module);
        var types = module.TypeSystem;
        var type = refs.Type("System.Runtime", "System", "Type");
        var assembly = refs.Type("System.Runtime", "System.Reflection", "Assembly");
        var exceptionType = refs.Type("System.Runtime", "System", "Exception");
        var environment = refs.Type("System.Runtime", "System", "Environment");
        var file = refs.Type("System.Runtime", "System.IO", "File");
        var textWriter = refs.Type("System.Runtime", "System.IO", "TextWriter");
        var methodBase = refs.Type("System.Runtime", "System.Reflection", "MethodBase");
        var methodInfo = refs.Type("System.Runtime", "System.Reflection", "MethodInfo");
        var runtimeTypeHandle = refs.Type("System.Runtime", "System", "RuntimeTypeHandle", isValueType: true);
        var loadContext = refs.Type("System.Runtime.Loader", "System.Runtime.Loader", "AssemblyLoadContext");
        var console = refs.Type("System.Console", "System", "Console");

        var method = new MethodDefinition(BootstrapMethodName,
            MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig, types.Void);
        var gameAssembly = new VariableDefinition(assembly);
        var loaderPath = new VariableDefinition(types.String);
        var exception = new VariableDefinition(exceptionType);
        method.Body.Variables.Add(gameAssembly);
        method.Body.Variables.Add(loaderPath);
        method.Body.Variables.Add(exception);
        method.Body.InitLocals = true;

        var il = method.Body.GetILProcessor();
        var ret = Instruction.Create(OpCodes.Ret);

        var tryStart = Instruction.Create(OpCodes.Ldtoken, entryType);
        il.Append(tryStart);
        il.Emit(OpCodes.Call, refs.Static(type, "GetTypeFromHandle", type, runtimeTypeHandle));
        il.Emit(OpCodes.Callvirt, refs.Instance(type, "get_Assembly", assembly));
        il.Emit(OpCodes.Stloc, gameAssembly);

        il.Emit(OpCodes.Ldstr, LoaderPathVariable);
        il.Emit(OpCodes.Call, refs.Static(environment, "GetEnvironmentVariable", types.String, types.String));
        il.Emit(OpCodes.Stloc, loaderPath);

        il.Emit(OpCodes.Ldloc, loaderPath);
        il.Emit(OpCodes.Call, refs.Static(file, "Exists", types.Boolean, types.String));
        var leaveNoMod = Instruction.Create(OpCodes.Leave, ret);
        il.Emit(OpCodes.Brfalse, leaveNoMod);

        il.Emit(OpCodes.Ldloc, gameAssembly);
        il.Emit(OpCodes.Call, refs.Static(loadContext, "GetLoadContext", loadContext, assembly));
        il.Emit(OpCodes.Ldloc, loaderPath);
        il.Emit(OpCodes.Callvirt, refs.Instance(loadContext, "LoadFromAssemblyPath", assembly, types.String));
        il.Emit(OpCodes.Ldstr, LoaderEntryType);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Callvirt, refs.Instance(assembly, "GetType", type, types.String, types.Boolean));
        il.Emit(OpCodes.Ldstr, LoaderEntryMethod);
        il.Emit(OpCodes.Callvirt, refs.Instance(type, "GetMethod", methodInfo, types.String));
        il.Emit(OpCodes.Ldnull);
        il.Emit(OpCodes.Ldnull);
        il.Emit(OpCodes.Callvirt, refs.Instance(methodBase, "Invoke", types.Object, types.Object, new ArrayType(types.Object)));
        il.Emit(OpCodes.Pop);
        il.Append(leaveNoMod);

        var catchStart = Instruction.Create(OpCodes.Stloc, exception);
        il.Append(catchStart);
        il.Emit(OpCodes.Call, refs.Static(console, "get_Error", textWriter));
        il.Emit(OpCodes.Ldloc, exception);
        il.Emit(OpCodes.Callvirt, refs.Instance(textWriter, "WriteLine", types.Void, types.Object));
        il.Emit(OpCodes.Leave, ret);
        il.Append(ret);

        method.Body.ExceptionHandlers.Add(new ExceptionHandler(ExceptionHandlerType.Catch)
        {
            CatchType = exceptionType,
            TryStart = tryStart,
            TryEnd = catchStart,
            HandlerStart = catchStart,
            HandlerEnd = ret,
        });

        return method;
    }

    /// <summary>
    /// Builds references to framework members against the reference facades (System.Runtime, ...) the game already
    /// compiles against, instead of importing from the installer's own runtime via reflection. Keeps the injected
    /// IL identical to what the C# compiler would emit and keeps the installer trim-safe.
    /// </summary>
    private sealed class FrameworkReferences(ModuleDefinition module)
    {
        private readonly AssemblyNameReference _systemRuntime =
            module.AssemblyReferences.FirstOrDefault(r => r.Name == "System.Runtime")
            ?? throw new InvalidOperationException("Game assembly does not reference System.Runtime.");

        public TypeReference Type(string facade, string ns, string name, bool isValueType = false) =>
            new(ns, name, module, Facade(facade), isValueType);

        public MethodReference Static(TypeReference declaringType, string name, TypeReference returnType, params TypeReference[] parameters) =>
            Method(declaringType, name, returnType, hasThis: false, parameters);

        public MethodReference Instance(TypeReference declaringType, string name, TypeReference returnType, params TypeReference[] parameters) =>
            Method(declaringType, name, returnType, hasThis: true, parameters);

        private static MethodReference Method(TypeReference declaringType, string name, TypeReference returnType, bool hasThis, TypeReference[] parameters)
        {
            var method = new MethodReference(name, returnType, declaringType) { HasThis = hasThis };
            foreach (var parameter in parameters)
            {
                method.Parameters.Add(new ParameterDefinition(parameter));
            }
            return method;
        }

        private AssemblyNameReference Facade(string name)
        {
            var existing = module.AssemblyReferences.FirstOrDefault(r => r.Name == name);
            if (existing != null)
            {
                return existing;
            }
            // Framework facades share System.Runtime's version and public key.
            var reference = new AssemblyNameReference(name, _systemRuntime.Version) { PublicKeyToken = _systemRuntime.PublicKeyToken };
            module.AssemblyReferences.Add(reference);
            return reference;
        }
    }
}
