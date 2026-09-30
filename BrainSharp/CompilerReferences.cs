using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BrainSharp.Processors
{
    public class CompilerReferences
    {
        public ILProcessor IlProcessor;
        
        public MethodReference PtrOutOfBoundsConstructor;
        
        public MethodReference ConsoleWriteMethod;
        public MethodReference ConsoleReadMethod;
        
        public FieldDefinition MemoryArrayField;
        public FieldDefinition MemoryPointerField;

        public TypeReference ExceptionHandlerType;
        public MethodReference ExceptionHandlerConstructor;
        public FieldReference ExceptionHandlerCurrentCharacterField;
        public FieldReference ExceptionHandlerCurrentMemoryPointerField;
        public FieldReference ExceptionHandlerCurrentCodeScopeField;
        
        public MethodReference MemoryByteConstructor;
        public MethodReference DisposeMethod;
        
        public MethodReference RuntimeConfigArgsMethod; 

        public VariableDefinition ExceptionHandlerVariable;
    }
}