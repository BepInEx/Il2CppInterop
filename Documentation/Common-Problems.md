# Common Problems

## Wrong mscorlib

You may be getting this error when running assembly generation:

```
Unhandled Exception: System.ArgumentNullException: Value cannot be null.
Parameter name: type
at Mono.Cecil.Mixin.CheckType(Object type)
at Mono.Cecil.ModuleDefinition.ImportReference(TypeReference type, IGenericParameterProvider context)
at Il2CppInterop.Generator.Passes.Pass60AddImplicitConversions.AddDelegateConversions(RewriteGlobalContext context)
at Il2CppInterop.Generator.Passes.Pass60AddImplicitConversions.DoPass(RewriteGlobalContext context)
at Il2CppInterop.Program.Main(UnhollowerOptions options)
at Il2CppInterop.Program.Main(String[] args)
```

This is because `--mscorlib` should point at the mod loader's mscorlib (or at the very least GAC mscorlib). It should not point to the dummy dll mscorlib.