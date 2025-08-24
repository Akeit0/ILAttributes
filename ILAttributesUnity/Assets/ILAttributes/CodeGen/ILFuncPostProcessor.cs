#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Mono.Collections.Generic;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.CompilationPipeline.Common.ILPostProcessing;
using UnityEngine;
using FieldAttributes = Mono.Cecil.FieldAttributes;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using MethodImplAttributes = Mono.Cecil.MethodImplAttributes;

// ReSharper disable StringLiteralTypo

namespace ILAttributes.CodeGen
{
    public static class ILUnsafeAccessorKind
    {
        public const byte Constructor = 0;
        public const byte Method = 1;
        public const byte Field = 2;
        public const byte StaticField = 3;
        public const byte StaticMethod = 4;
    }

    public class ILAttributesPostProcessor : ILPostProcessor
    {
        public override ILPostProcessor GetInstance() => this;

        public override bool WillProcess(ICompiledAssembly compiledAssembly)
        {
            if (compiledAssembly.Name == "ILAttributes") return true;
            return compiledAssembly.References.Any(f => Path.GetFileName(f) == "ILAttributes.dll");
        }

        static FieldReference CreateFieldReference(FieldDefinition definition)
        {
            var declaringType = new GenericInstanceType(definition.DeclaringType);
            foreach (var parameter in definition.DeclaringType.GenericParameters)
            {
                declaringType.GenericArguments.Add(parameter);
            }

            return new FieldReference(definition.Name, definition.FieldType, declaringType);
        }


        public override ILPostProcessResult Process(ICompiledAssembly compiledAssembly)
        {
            if (!WillProcess(compiledAssembly))
                return null;

            List<DiagnosticMessage> diagnostics = new List<DiagnosticMessage>();


            void AddMessage(object message, DiagnosticType diagnosticType = DiagnosticType.Warning)
            {
                diagnostics.Add(new DiagnosticMessage
                {
                    DiagnosticType = diagnosticType,
                    MessageData = message.ToString(),
                });
            }


            using var assemblyDefinition =
                ILPostProcessUtility.AssemblyDefinitionFor(compiledAssembly);
            var mainModule = assemblyDefinition.MainModule;
            var voidType = mainModule.ImportReference(typeof(void));
            if (compiledAssembly.Name == "ILAttributes")
            {
                foreach (var typeDefinition in mainModule.Types)
                {
                    if (typeDefinition.IsValueType && typeDefinition.CustomAttributes.Any(x =>
                            x.Constructor.DeclaringType.Name == "ILRefGenAttribute"))
                    {
                        if (ILPostProcessUtility.Core == null)
                        {
                            AddMessage("CoreLib is null");
                        }
                        else
                        {
                            var def = ILPostProcessUtility.Core.GetType("System", "ByReference`1");
                            if (def != null)
                            {
                                var b = mainModule.ImportReference(def);
                                var genericInstanceType = new GenericInstanceType(b);
                                var ctorDef =
                                    def.Methods.First(x => x.IsConstructor); //new ByRef<T>(ref T value)
                                var byReferenceCtor =
                                    new MethodReference(".ctor", voidType, genericInstanceType)
                                    {
                                        CallingConvention = ctorDef.CallingConvention,
                                        HasThis = ctorDef.HasThis,
                                        ExplicitThis = ctorDef.ExplicitThis,
                                    };
                                genericInstanceType.GenericArguments.Add(typeDefinition.GenericParameters[0]);
                                var fieldDef = new FieldDefinition("_ref", FieldAttributes.Private,
                                    genericInstanceType);
                                typeDefinition.Fields.Add(fieldDef);
                                var field = CreateFieldReference(fieldDef);
                                var ctor = typeDefinition.Methods.First(x => x.IsConstructor);
                                byReferenceCtor.Parameters.Add(
                                    new ParameterDefinition(ctor.Parameters[0].ParameterType));
                                var processor = ctor.Body.GetILProcessor();
                                processor.Emit(OpCodes.Ldarg_0);
                                processor.Emit(OpCodes.Ldarg_1);
                                processor.Emit(OpCodes.Newobj, byReferenceCtor);
                                processor.Emit(OpCodes.Stfld, field);
                                processor.Emit(OpCodes.Ret);
                                var getDef = def.Methods.First(x => x.Name == "get_Value");

                                var getRef =
                                    typeDefinition.Properties.First(x => x.Name == "Value")
                                        .GetMethod; //First(x => x.Name == "Value")
                                var byReferenceGetValue =
                                    new MethodReference("get_Value", getRef.ReturnType, genericInstanceType)
                                    {
                                        CallingConvention = getDef.CallingConvention,
                                        HasThis = getDef.HasThis,
                                        ExplicitThis = getDef.ExplicitThis,
                                    };
                                processor = getRef.Body.GetILProcessor();
                                processor.Clear();
                                processor.Emit(OpCodes.Ldarg_0);
                                processor.Emit(OpCodes.Ldflda, field);
                                processor.Emit(OpCodes.Call, byReferenceGetValue);
                                processor.Emit(OpCodes.Ret);
                                // AddMessage(def);
                            }
                        }
                    }
                }
            }

            foreach (var typeDefinition in mainModule.Types)
            {
                if (typeDefinition.HasCustomAttributes &&
                    typeDefinition.CustomAttributes.Any(x => x.Constructor.DeclaringType.Name == "ILProcessAttribute"))
                {
                    foreach (var methodDefinition in typeDefinition.Methods)
                    {
                        var hasViewAttribute = false;
                        foreach (var customAttribute in methodDefinition.CustomAttributes)
                        {
                            var name = customAttribute.Constructor.DeclaringType.Name;
                            try
                            {
                                if (name == "ILViewAttribute")
                                {
                                    hasViewAttribute = true;

                                    break;
                                }

                                if (name == "ILRetIdentity")
                                {
                                    var body = methodDefinition.Body;
                                    var processor = body.GetILProcessor();
                                    processor.Clear();
                                    processor.Emit(OpCodes.Ldarg_0);
                                    processor.Emit(OpCodes.Ret);
                                    processor.Body.Optimize();
                                    break;
                                }

                                if (name == "ILUnsafeAccessorAttribute")
                                {
                                    var unsafeAccessorKind =
                                        (byte)customAttribute.ConstructorArguments[0].Value;
                                    var accessName = ((string)customAttribute.ConstructorArguments[1].Value);
                                    TypeReference declaringType = null;
                                    if (customAttribute.ConstructorArguments.Count > 2)
                                    {
                                        declaringType = (TypeReference)customAttribute.ConstructorArguments[2].Value;
                                        if (0 < declaringType.GenericParameters.Count)
                                        {
                                            var genericInstanceType = new GenericInstanceType(declaringType);
                                            foreach (var genericParameter in methodDefinition.GenericParameters)
                                            {
                                                genericInstanceType.GenericArguments.Add(genericParameter);
                                            }
                                        
                                            declaringType = genericInstanceType;
                                        }
									}

                                    var parameters = methodDefinition.Parameters;
                                    bool declaringTypeOnAttribute = declaringType != null;
                                    if (declaringType == null)
                                    {
                                        declaringType = methodDefinition.Parameters[0].ParameterType;
                                        if (declaringType.IsByReference)
                                        {
                                            declaringType = ((ByReferenceType)declaringType).ElementType;
                                        }
                                    }

                                    var body = methodDefinition.Body;

                                    var processor = body.GetILProcessor();
                                    processor.Clear();
                                    switch (unsafeAccessorKind)
                                    {
                                        case ILUnsafeAccessorKind.Constructor:
                                        {
                                            declaringType = methodDefinition.ReturnType;
                                            var converter = new GenericTypeConverter(declaringType, methodDefinition);

                                            var method = new MethodReference(".ctor", voidType,
                                                declaringType)
                                            {
                                                HasThis = true
                                            };
                                            for (int i = 0; i < parameters.Count; i++)
                                            {
                                                method.Parameters.Add(
                                                    new ParameterDefinition(
                                                        converter.Convert(parameters[i].ParameterType)));

                                                processor.Emit(OpCodes.Ldarg, i);
                                            }

                                            processor.Emit(OpCodes.Newobj, method);
                                            break;
                                        }
                                        case ILUnsafeAccessorKind.Method:
                                        {
                                            var converter = new GenericTypeConverter(declaringType, methodDefinition);
                                            var returnType = converter.Convert(methodDefinition.ReturnType);
											var method = new MethodReference(accessName, returnType, declaringType)
                                            {
                                                HasThis = true
                                            };

											if ( methodDefinition.GenericParameters.Count > converter.GenericParameterCount )
											{
												var genericParameters = methodDefinition.GenericParameters;
												var genericMethod = new GenericInstanceMethod( method );
												for ( var i = converter.GenericParameterCount; i < genericParameters.Count; i++ )
												{
													var genericParameter = genericParameters[i];
													method.GenericParameters.Add( new GenericParameter( genericParameter.Name, method ) );
													genericMethod.GenericArguments.Add( genericParameter );
												}

												converter.GenericParametersOfMethod = method.GenericParameters;
												method = genericMethod;
												// method = methodDefinition.Module.ImportReference( genericMethod );
											}

											processor.Emit(OpCodes.Ldarg_0);
											for ( var i = 1; i < parameters.Count; i++ )
											{
												var paramType = converter.Convert( parameters[i].ParameterType );
												method.Parameters.Add( new ParameterDefinition( paramType ) );
												processor.Emit( OpCodes.Ldarg, i );
											}

											method = methodDefinition.Module.ImportReference( method );
											processor.Emit( OpCodes.Call, method );
                                            break;
                                        }

                                        case ILUnsafeAccessorKind.Field:
                                        {
                                            var fieldType = ((ByReferenceType)methodDefinition.ReturnType).ElementType;
                                            if (fieldType.ContainsGenerics())
                                            {
                                                var converter =
                                                    new GenericTypeConverter(declaringType, methodDefinition);
                                                fieldType = converter.Convert(fieldType);
                                            }

											FieldReference fieldReference = new FieldReference( accessName,
												fieldType,
												declaringType );
											
                                            processor.Emit(OpCodes.Ldarg_0);
                                            processor.Emit(OpCodes.Ldflda, fieldReference);
                                            break;
                                        }
                                        case ILUnsafeAccessorKind.StaticField:
                                        {
                                            var fieldType = methodDefinition.ReturnType.GetElementType();
                                            if ((fieldType.IsGenericInstance ||
                                                 fieldType.IsGenericParameter))
                                            {
                                                var converter =
                                                    new GenericTypeConverter(declaringType, methodDefinition);
                                                fieldType = converter.Convert(fieldType);
                                            }

                                            var field = new FieldReference(accessName,
                                                fieldType,
                                                declaringType);

                                            processor.Emit(OpCodes.Ldsflda, field);
                                            break;
                                        }
                                        case ILUnsafeAccessorKind.StaticMethod:
                                        {
                                            var converter = new GenericTypeConverter(declaringType, methodDefinition);
                                            var method = new MethodReference(accessName,
                                                converter.Convert(methodDefinition.ReturnType),
                                                declaringType);
                                            for (int i = declaringTypeOnAttribute ? 0 : 1; i < parameters.Count; i++)
                                            {
                                                method.Parameters.Add(
                                                    new ParameterDefinition(converter.Convert(parameters[i]
                                                        .ParameterType)));
                                                processor.Emit(OpCodes.Ldarg, i);
                                            }

                                            var genericParameters = methodDefinition.GenericParameters;
                                            if (0 < genericParameters.Count && converter.GenericParameterCount !=
                                                genericParameters.Count)
                                            {
                                                if (converter.GenericParameterCount != 0)
                                                    throw new NotSupportedException(
                                                        $"GenericMethod of GenericClass is not supported");
                                                var genericMethod = new GenericInstanceMethod(method);
                                                foreach (var genericParameter in genericParameters)
                                                {
                                                    method.GenericParameters.Add(
                                                        new GenericParameter(genericParameter.Name, method));
                                                    genericMethod.GenericArguments.Add(genericParameter);
                                                }

                                                method = genericMethod;
                                            }

                                            processor.Emit(OpCodes.Call, method);
                                            break;
                                        }
                                    }

                                    processor.Emit(OpCodes.Ret);
                                    processor.Body.Optimize();
                                    // AddMessage(methodDefinition.ToString());
                                    break;
                                }

                                if (name == "ILStaticFieldAccessAttribute")
                                {
                                    var declaringType = (TypeReference)customAttribute.ConstructorArguments[0].Value;
                                    var fieldName = ((string)customAttribute.ConstructorArguments[1].Value);
                                    var body = methodDefinition.Body;
                                    var processor = body.GetILProcessor();
                                    processor.Clear();
                                    processor.Emit(OpCodes.Ldarg_0);
                                    if (methodDefinition.ReturnType.IsByReference)
                                    {
                                        var filedRef = new FieldReference(fieldName,
                                            methodDefinition.ReturnType.GetElementType(),
                                            declaringType);
                                        processor.Emit(OpCodes.Ldflda, filedRef);
                                    }
                                    else
                                    {
                                        var filedRef = new FieldReference(fieldName, methodDefinition.ReturnType,
                                            methodDefinition.Parameters[0].ParameterType);
                                        processor.Emit(OpCodes.Ldfld, filedRef);
                                    }

                                    processor.Emit(OpCodes.Ret);
                                    processor.Body.Optimize();
                                    break;
                                }

                                if (name == "ILStaticMethodAccessAttribute")
                                {
                                    var declaringType = (TypeReference)customAttribute.ConstructorArguments[0].Value;
                                    var method = ((string)customAttribute.ConstructorArguments[1].Value);
                                    var returnType = methodDefinition.ReturnType;
                                    var methodRef = new MethodReference(method, returnType, declaringType);
                                    for (int i = 0; i < methodDefinition.Parameters.Count; i++)
                                    {
                                        methodRef.Parameters.Add(methodDefinition.Parameters[i]);
                                    }

                                    var body = methodDefinition.Body;
                                    var processor = body.GetILProcessor();
                                    processor.Clear();
                                    processor.Emit(OpCodes.Ldarg_0);
                                    processor.Emit(OpCodes.Call, methodRef);
                                    processor.Emit(OpCodes.Ret);
                                    processor.Body.Optimize();
                                    break;
                                }

                                if (name == "ILInlineAttribute")
                                {
                                    Emit(methodDefinition, customAttribute);
                                    break;
                                }
                            }
                            catch (Exception e)
                            {
                                var body = methodDefinition.Body;
                                var processor = body.GetILProcessor();
                                processor.Clear();
                                diagnostics.Add(new DiagnosticMessage
                                {
                                    DiagnosticType = DiagnosticType.Error,
                                    MessageData = @$"
DLL: {methodDefinition.Module.Name} Type: {methodDefinition.DeclaringType}  Method: {methodDefinition.Name}  Attribute: {name}
Message: {e.Message} 
StackTrace: {e.StackTrace}",
                                    File = typeDefinition.Module.FileName,
                                    Line = 0,
                                    Column = 0
                                });
                            }
                        }

                        if (hasViewAttribute)
                        {
                            var body = methodDefinition.Body;
                            var processor = body.GetILProcessor();
                            var stringBuilder = new StringBuilder();
                            stringBuilder.Append("ILView : ");
                            stringBuilder.AppendLine(methodDefinition.ToString());
                            foreach (var instruction in processor.Body.Instructions)
                            {
                                stringBuilder.AppendLine(instruction.ToString());
                            }

                            diagnostics.Add(new()
                            {
                                DiagnosticType = DiagnosticType.Warning,
                                MessageData = stringBuilder.ToString(),
                                File = typeDefinition.Module.FileName,
                                Line = 0,
                                Column = 0
                            });
                        }
                    }
                }
            }

            return ILPostProcessUtility.GetResult(assemblyDefinition, diagnostics);
        }


        static unsafe ReadOnlySpan<char> AsSpan(ReadOnlySpan<char> str) => str;

        static unsafe string AsSpan(string str) => str;

        private void Emit(MethodDefinition definition, CustomAttribute customAttribute)
        {
            var processor = definition.Body.GetILProcessor();
            var opCodes =
                ((string)customAttribute.ConstructorArguments[0].Value).Split(new[] { " ", Environment.NewLine },
                    StringSplitOptions.RemoveEmptyEntries);

            var types = customAttribute.ConstructorArguments.Count > 1
                ? customAttribute.ConstructorArguments[1].Value as CustomAttributeArgument[]
                : null;

            var typeRefs = types == null
                ? Array.Empty<TypeReference>()
                : types.Select(f => (TypeReference)f.Value).ToArray();

            bool TryGetTypeReference(string name, out TypeReference typeReference)
            {
                typeReference = GetTypeReference(name, false);
                return typeReference == null;
            }

            TypeReference GetTypeReference(string name, bool throwOnError = true)
            {
                var nameSpan = AsSpan(name);
                if (name.StartsWith('@'))
                {
                    if (name.EndsWith('&'))
                    {
                        return typeRefs[int.Parse(nameSpan[1..^1])].MakeByReferenceType();
                    }

                    return typeRefs[int.Parse(nameSpan[1..])];
                }
                else
                {
                    if (name.StartsWith('!'))
                    {
                        if (name.EndsWith('&'))
                        {
                            return typeRefs[int.Parse(nameSpan[2..^1])].MakeByReferenceType();
                        }

                        return definition.GenericParameters.FirstOrDefault(x => x.Name == name[2..]);
                    }

                    if (throwOnError) throw new NotSupportedException(name);
                    return null;
                }
            }

            processor.Body.Variables.Clear();
            processor.Body.Instructions.Clear();
            //var first = opCodes[0];
            /*if(first==".locals")
            {
                processor.Body.Variables.Add(new VariableDefinition(_corlib.ImportReference(typeof(int))));

            }*/
            for (var index = 0; index < opCodes.Length; index++)
            {
                var opCode = opCodes[index];
                switch (opCode)
                {
                    case "nop":
                        processor.Emit(OpCodes.Nop);
                        break;
                    case "pop":
                        processor.Emit(OpCodes.Pop);
                        break;
                    case "dup":
                        processor.Emit(OpCodes.Dup);
                        break;
                    case "break":
                        processor.Emit(OpCodes.Break);
                        break;
                    /*case "ldloc.0":
                        processor.Emit(OpCodes.Ldloc_0);
                        break;
                    case "ldloc.1":
                        processor.Emit(OpCodes.Ldloc_1);
                        break;
                    case "ldloc.2":
                        processor.Emit(OpCodes.Ldloc_2);
                        break;
                    case "ldloc.3":
                        processor.Emit(OpCodes.Ldloc_3);
                        break;
                    case "stloc.1":
                        processor.Emit(OpCodes.Stloc_1);
                        break;
                    case "stloc.2":
                        processor.Emit(OpCodes.Stloc_2);
                        break;
                    case "stloc.3":
                        processor.Emit(OpCodes.Stloc_3);
                        break;*/
                    case "ldnull":
                        processor.Emit(OpCodes.Ldnull);
                        break;
                    case "jmp":
                        processor.Emit(OpCodes.Jmp);
                        break;
                    case "call":
                    {
                        var returnTypeIndex = GetTypeReference(opCodes[++index]);
                        var declareTypeIndex = GetTypeReference(opCodes[++index]);
                        var methodText = opCodes[++index];
                        var i = index;
                        var methodRef = new MethodReference(methodText, returnTypeIndex,
                            declareTypeIndex);
                        var parameters = methodRef.Parameters;
                        while (TryGetTypeReference(opCodes[++i], out var argType))
                        {
                            parameters.Add(new ParameterDefinition(argType));
                        }

                        index = i - 1;
                        processor.Emit(OpCodes.Call, methodRef);
                        break;
                    }
                    case "ldfld":
                    {
                        var fieldType = GetTypeReference(opCodes[++index]);
                        var declareType = GetTypeReference(opCodes[++index]);
                        var fieldText = opCodes[++index];
                        processor.Emit(OpCodes.Ldfld,
                            new FieldReference(fieldText, fieldType, declareType));
                        break;
                    }
                    case "ldflda":
                    {
                        var fieldType = GetTypeReference(opCodes[++index]);
                        var declareType = GetTypeReference(opCodes[++index]);
                        var fieldText = opCodes[++index];
                        processor.Emit(OpCodes.Ldflda,
                            new FieldReference(fieldText, fieldType, declareType));
                        break;
                    }
                    case "sizeof":
                    {
                        var type = GetTypeReference(opCodes[++index]);
                        processor.Emit(OpCodes.Sizeof, type);
                        break;
                    }
                    case "ldobj":
                    {
                        var type = GetTypeReference(opCodes[++index]);
                        processor.Emit(OpCodes.Ldobj, type);
                        break;
                    }
                    case "stobj":
                    {
                        var type = GetTypeReference(opCodes[++index]);
                        processor.Emit(OpCodes.Stobj, type);
                        break;
                    }
                    case "ldc.i4":
                        processor.Emit(OpCodes.Ldc_I4, int.Parse(opCodes[++index]));
                        break;
                    case "ldc.i4.0":
                        processor.Emit(OpCodes.Ldc_I4_0);
                        break;
                    case "ldc.i4.1":
                        processor.Emit(OpCodes.Ldc_I4_1);
                        break;
                    case "ldc.i4.2":
                        processor.Emit(OpCodes.Ldc_I4_2);
                        break;

                    case "ldc.i4.3":
                        processor.Emit(OpCodes.Ldc_I4_3);
                        break;
                    case "ldc.i4.4":
                        processor.Emit(OpCodes.Ldc_I4_4);
                        break;
                    case "ldc.i4.5":
                        processor.Emit(OpCodes.Ldc_I4_5);
                        break;
                    case "ldc.i4.6":
                        processor.Emit(OpCodes.Ldc_I4_6);
                        break;
                    case "ldc.i4.7":
                        processor.Emit(OpCodes.Ldc_I4_7);
                        break;
                    case "ldc.i4.8":
                        processor.Emit(OpCodes.Ldc_I4_8);
                        break;
                    case "ldc.i8":
                        processor.Emit(OpCodes.Ldc_I8, long.Parse(opCodes[++index]));
                        break;
                    case "ldstr":
                        processor.Emit(OpCodes.Ldc_I8, Regex.Unescape(opCodes[++index]));
                        index++;
                        break;
                    case "ldarg":
                        processor.Emit(OpCodes.Ldarg, ushort.Parse(opCodes[++index]));
                        break;
                    case "ldarg.s":
                        processor.Emit(OpCodes.Ldarg_S, byte.Parse(opCodes[++index]));
                        break;
                    case "ldarg.0":
                        processor.Emit(OpCodes.Ldarg_0);
                        break;
                    case "ldarg.1":
                        processor.Emit(OpCodes.Ldarg_1);
                        break;
                    case "ldarg.2":
                        processor.Emit(OpCodes.Ldarg_2);
                        break;
                    case "ldarg.3":
                        processor.Emit(OpCodes.Ldarg_3);
                        break;
                    case "and":
                        processor.Emit(OpCodes.And);
                        break;
                    case "add":
                        processor.Emit(OpCodes.Add);
                        break;
                    case "add.ovf":
                        processor.Emit(OpCodes.Add_Ovf);
                        break;
                    case "add.ovf.un":
                        processor.Emit(OpCodes.Add_Ovf_Un);
                        break;
                    case "sub":
                        processor.Emit(OpCodes.Sub);
                        break;
                    case "mul":
                        processor.Emit(OpCodes.Mul);
                        break;
                    case "div":
                        processor.Emit(OpCodes.Div);
                        break;
                    case "conv.u":
                        processor.Emit(OpCodes.Conv_U);
                        break;
                    case "conv.u1":
                        processor.Emit(OpCodes.Conv_U1);
                        break;
                    case "conv.u2":
                        processor.Emit(OpCodes.Conv_U2);
                        break;
                    case "conv.u4":
                        processor.Emit(OpCodes.Conv_U4);
                        break;
                    case "conv.u8":
                        processor.Emit(OpCodes.Conv_U8);
                        break;
                    case "conv.i":
                        processor.Emit(OpCodes.Conv_I);
                        break;
                    case "conv.i1":
                        processor.Emit(OpCodes.Conv_I1);
                        break;
                    case "conv.i2":
                        processor.Emit(OpCodes.Conv_I2);
                        break;
                    case "conv.i4":
                        processor.Emit(OpCodes.Conv_I4);
                        break;
                    case "conv.i8":
                        processor.Emit(OpCodes.Conv_I8);
                        break;
                    case "conv.r4":
                        processor.Emit(OpCodes.Conv_R4);
                        break;
                    case "conv.r8":
                        processor.Emit(OpCodes.Conv_R8);
                        break;
                    case "conv.r.un":
                        processor.Emit(OpCodes.Conv_R_Un);
                        break;
                    case "cpblk":
                        processor.Emit(OpCodes.Cpblk);
                        break;
                    case "initblk":
                        processor.Emit(OpCodes.Initblk);
                        break;
                    case "ldind.i":
                        processor.Emit(OpCodes.Ldind_I);
                        break;
                    case "unbox":
                        processor.Emit(OpCodes.Unbox);
                        break;
                    case "unbox.any":
                        processor.Emit(OpCodes.Unbox_Any);
                        break;
                    case "unaligned.":
                        processor.Emit(OpCodes.Unaligned, byte.Parse(opCodes[++index]));
                        break;
                    case "ceq":
                        processor.Emit(OpCodes.Ceq);
                        break;
                    case "cgt":
                        processor.Emit(OpCodes.Cgt);
                        break;
                    case "cgt.un":
                        processor.Emit(OpCodes.Cgt_Un);
                        break;
                    case "clt":
                        processor.Emit(OpCodes.Clt);
                        break;
                    case "clt.un":
                        processor.Emit(OpCodes.Clt_Un);
                        break;
                    case "ldlen":
                        processor.Emit(OpCodes.Ldlen);
                        break;

                    case "ret":
                        processor.Emit(OpCodes.Ret);
                        break;
                    default: throw new NotSupportedException(opCode);
                }
            }

            processor.Body.Optimize();
        }
    }
}
#nullable restore