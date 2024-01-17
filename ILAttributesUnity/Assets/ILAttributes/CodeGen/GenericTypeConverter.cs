using System;
using System.Linq;
using Mono.Cecil;
using Mono.Collections.Generic;

namespace ILAttributes.CodeGen
{
    public struct GenericTypeConverter
    {
        //public TypeReference TypeReference;
        //  TypeDefinition typeDefinition;
        // Collection<GenericParameter> genericParametersOfType;
        Collection<GenericParameter> genericParametersOfTypeDef;

        //  Collection<GenericParameter> genericParametersOfMethod;
        //  Collection<GenericParameter> genericParametersOfMethodDeclaringType;
        public bool IsGenericInstance => genericParametersOfTypeDef != null;

        public int GenericParameterCount => genericParametersOfTypeDef?.Count ?? 0;

        // public int FullGenericParameterCount =>GenericParameterCount+ genericParametersOfMethodDeclaringType?.Count ?? 0;
        static bool HasSameName(GenericParameter genericParameter, Collection<GenericParameter> genericParameters)
        {
            foreach (var defGenericParameter in genericParameters)
            {
                if (defGenericParameter.Name == genericParameter.Name)
                    return true;
            }

            return false;
        }

        static bool HasSameName(Collection<GenericParameter> a, Collection<GenericParameter> b)
        {
            foreach (var genericParameterA in a)
            {
                foreach (var genericParameterB in b)
                {
                    if (genericParameterA.Name == genericParameterB.Name)
                        return true;
                }
            }

            return false;
        }

        // bool TryGetTypeDefParam(GenericParameter parameter,out GenericParameter defParam)
        // {
        //     defParam = default;
        //     if(genericParametersOfType==null) return false;
        //     for (int i = 0; i < genericParametersOfType.Count; i++)
        //     {
        //         if(parameter.Name==genericParametersOfType[i].Name)
        //         {
        //             defParam=genericParametersOfTypeDef[i];
        //             return true;
        //         }
        //     }
        //     return false;
        // }
        public GenericTypeConverter(TypeReference typeReference, MethodDefinition methodReference)
        {
            this = default;
            if (typeReference.IsGenericInstance)
            {
                var genericType = (GenericInstanceType)typeReference;
                foreach (var genericArgument in genericType.GenericArguments)
                {
                    if (!genericArgument.IsGenericParameter)
                    {
                        throw new NotSupportedException("Closed Generic Type is not supported");
                    }
                }
                
                // genericParametersOfType=typeReference.GetElementType().GenericParameters;
                var typeDefinition = typeReference.Resolve();
                genericParametersOfTypeDef = typeDefinition.GenericParameters;
                // genericParametersOfMethod=methodReference.GenericParameters;
                // genericParametersOfMethodDeclaringType = methodReference.DeclaringType.GenericParameters;
                // foreach (var parameterOfMethod in methodReference.Parameters) {
                //     if(parameterOfMethod.ParameterType.HasGenericParameters)
                //     foreach (var genericParameterOfType in genericParametersOfType)
                //     {
                //         if(genericParameterOfType.Name==parameterOfMethod.ParameterType.Name)break;
                //     }
                //     throw new NotSupportedException("GenericMethod of GenericClass is not supported"+parameterOfMethod.Name+genericParametersOfType[0].Name);
                // }
            }

            
        }

        public TypeReference Convert(TypeReference typeReference)
        {
            if (genericParametersOfTypeDef == null) return typeReference;
            if (!typeReference.ContainsGenerics()) return typeReference;
            if (typeReference is TypeSpecification)
            {
                if (typeReference.IsArray)
                {
                    var arrayType = (ArrayType)typeReference;
                    var elementType = arrayType.ElementType;
                    var elementType2 = Convert(elementType);
                    if (elementType2 == elementType)
                    {
                        return typeReference;
                    }

                    return new ArrayType(elementType2, arrayType.Rank);
                }

                if (typeReference.IsByReference)
                {
                    var byReferenceType = (ByReferenceType)typeReference;
                    var elementType = byReferenceType.ElementType;
                    var elementType2 = Convert(elementType);
                    if (elementType2 == elementType)
                    {
                        return typeReference;
                    }

                    return new ByReferenceType(elementType2);
                }

                if (typeReference.IsPointer)
                {
                    var pointerType = (PointerType)typeReference;
                    var elementType = pointerType.ElementType;
                    var elementType2 = Convert(elementType);
                    if (elementType2 == elementType)
                    {
                        return typeReference;
                    }

                    return new PointerType(elementType2);
                }
            }

            if (typeReference.IsGenericParameter)
            {
                var genericParameter = (GenericParameter)typeReference;
                foreach (var defGenericParameter in genericParametersOfTypeDef)
                {
                    if (defGenericParameter.Name == genericParameter.Name)
                        return defGenericParameter;
                }

                throw new NotSupportedException("GenericParameter of GenericClass is not supported" +
                                                genericParameter.Name);
                //return typeReference;
            }

            if (typeReference.IsGenericInstance)
            {
                var genericInstanceType = (GenericInstanceType)typeReference;
                if (!HasSameName(genericInstanceType.GenericParameters, genericParametersOfTypeDef))
                {
                    return typeReference;
                }

                var genericInstanceType2 = new GenericInstanceType(typeReference.Resolve());
                foreach (var genericArgument in genericInstanceType.GenericArguments)
                {
                    genericInstanceType2.GenericArguments.Add(Convert(genericArgument));
                }

                return genericInstanceType2;
            }

            return typeReference;
        }
    }
}