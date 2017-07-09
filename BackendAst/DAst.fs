﻿module DAst

open Antlr.Runtime.Tree
open Antlr.Runtime
open System
open System.Numerics
open FsUtils
open CommonTypes
//open Constraints





/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////// ASN1 VALUES DEFINITION    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

type IntegerValue         = BigInteger
type RealValue            = double
type StringValue          = string
type BooleanValue         = bool
type BitStringValue       = string
type OctetStringValue     = list<byte>
type EnumValue            = string
type NullValue            = unit
type SeqOfValue           = list<Asn1Value>
and SeqValue              = list<NamedValue>
and ChValue               = NamedValue
and RefValue              = ((string*string)*Asn1Value)

and NamedValue = {
    name        : string
    Value       : Asn1Value
}

and Asn1Value = {
    kind : Asn1ValueKind
    loc  : SrcLoc
    id   : ReferenceToValue
}

and Asn1ValueKind =
    | IntegerValue          of IntegerValue    
    | RealValue             of RealValue       
    | StringValue           of StringValue     
    | BooleanValue          of BooleanValue    
    | BitStringValue        of BitStringValue  
    | OctetStringValue      of OctetStringValue
    | EnumValue             of EnumValue       
    | SeqOfValue            of SeqOfValue      
    | SeqValue              of SeqValue        
    | ChValue               of ChValue         
    | NullValue             of NullValue
    | RefValue              of RefValue   

//type Asn1GenericValue = Asn1Value

type ProgrammingLanguage =
    |C
    |Ada


type FuncParamType =
  | VALUE       of string
  | POINTER     of string
  | FIXARRAY    of string

type ExpOrStatement =
    | Expression 
    | Statement  

type LocalVariable =
    | SequenceOfIndex       of int*int option        //i index, initialValue
    | IntegerLocalVariable  of string*int option     //variable name, initialValue
    | Asn1SIntLocalVariable of string*int option     //variable name, initialValue
    | Asn1UIntLocalVariable of string*int option     //variable name, initialValue
    | FlagLocalVariable     of string*int option     //variable name, initialValue
    | AcnInsertedChild      of string*string         //variable name, type 



         //Emit_local_variable_SQF_Index(nI, bHasInitalValue)::="I<nI>:Integer<if(bHasInitalValue)>:=1<endif>;"

type TypeOrSubsType =
    | TYPE
    | SUBTYPE


type TypeDefinitionCommon = {
    // the name of the type C or Ada type. Defives from ASN.1 Type Assignment name.
    // Eg. for MyInt4 ::= INTEGER(0..15|20|25)
    // name will be MyInt4 
    name                : string            

    // used only in Ada backend. 
    typeOrSubsType      : TypeOrSubsType    

    //In above example, in C the typeDefinitionBody is : asn1SccSint
    //              in Ada                          is : adaasn1rtl.Asn1Int range 0..25
    // for a complext type e.g. sequence 
    // in C   is the struct { ... fields ...}
    // in Ada is the IS RECORD ... fields ... END RECORD
    // Ada does not allow nested type defintions. Therefore when called with NESTED_DEFINITION_SCOPE (i.e. from a SEQUENCE) the name of the type is returned
    //typeDefinitionBody  : string            

    // Used only for Strings and is the size of the string (plus one for the null terminated character)
    // It is usefull only in C due to the fact that the size of the array is not part of the type definition body but follows the the type name
    // i.e. for the ASN.1 type   STRCAPS   ::= IA5String (SIZE(1..100)) (FROM ("A".."Z"))	
    // the C definition is : typedef char STRCAPS[101];
    // If the definition was typedef char[101] STRCAPS; (which is the case for all other languages Ada, C#, Java etc) then we wouldn't need it
    arraySize           : int option      
    
    // the complete definition of the type
    // e.g. C : typedef asn1SccSint MyInt4;
    // and Ada: SUBTYPE MyInt4 IS adaasn1rtl.Asn1Int range 0..25;    
    completeDefinition  : string  


    // Ada does not allow nested type definitions.
    // Therefore The following type MySeq { a INTEGER, innerSeq SEQUENCE {b REAL}}
    // must be declared as if it was defined MySeq_innerSeq SEQUENCE {b REAL} MySeq { a INTEGER, innerSeq MySeq_innerSeq}
    // in this case it will be : MySeq_innerSeq 
    typeDefinitionBodyWithinSeq : string

    //the complete deffinition of inner complex types that must be declared
    //before this type
    completeDefinitionWithinSeq : string option
}

type ErroCode = {
    errCodeValue    : int
    errCodeName     : string
}

type BaseTypesEquivalence<'T> = {
    typeDefinition  : 'T option
    uper            : 'T option
    acn             : 'T option
}
        
(*
Generates initialization statement(s) that inititalize the type with the given Asn1GeneticValue.
*)            
type InitFunction = {
    initFuncName            : string option               // the name of the function
    initFunc                : string option               // the body of the function
    initFuncDef             : string option               // function definition in header file
    initFuncBody            : FuncParamType  -> Asn1ValueKind -> string                      // returns the statement(s) that initialize this type
}


type IsEqualBody =
    | EqualBodyExpression       of (string -> string -> (string*(LocalVariable list)) option)
    | EqualBodyStatementList    of (string -> string -> (string*(LocalVariable list)) option)

type IsEqualBody2 =
    | EqualBodyExpression2       of (string -> string -> string -> (string*(LocalVariable list)) option)
    | EqualBodyStatementList2    of (string -> string -> string -> (string*(LocalVariable list)) option)

type EqualFunction = {
    isEqualFuncName     : string option               // the name of the equal function. 
    isEqualFunc         : string option               // the body of the equal function
    isEqualFuncDef      : string option
    isEqualBody         : IsEqualBody                 // a function that  returns an expression or a statement list
    isEqualBody2        : IsEqualBody2
}

type AlphaFunc   = {
    funcName            : string
    funcBody            : string
}

type AnonymousVariable = {
    valueName           : string
    valueExpresion      : string
    typeDefinitionName  : string
}

type IsValidFunction = {
    errCodes            : ErroCode list
    funcName            : string option               // the name of the function. Valid only for TASes)
    func                : string option               // the body of the function
    funcDef             : string option               // function definition in header file
    funcExp             : (string -> string) option   // return a single boolean expression
    funcBody            : string -> string            //returns a list of validations statements
    funcBody2           : string -> string -> string  //like funBody but with two arguement p and accessOper ( i.e. '->' or '.')
    alphaFuncs          : AlphaFunc list  
    localVariables      : LocalVariable list
    anonymousVariables  : AnonymousVariable  list      //list with the anonymous asn1 values used in constraints and which must be declared.
                                                       //these are the bit and octet string values which cannot be expressed as single primitives in C/Ada
}

type UPERFuncBodyResult = {
    funcBody            : string
    errCodes            : ErroCode list
    localVariables      : LocalVariable list
}
type UPerFunction = {
    funcName            : string option               // the name of the function
    func                : string option               // the body of the function
    funcDef             : string option               // function definition in header file
    funcBody            : FuncParamType -> (UPERFuncBodyResult option)            // returns a list of validations statements
}

type AcnFuncBodyResult = {
    funcBody            : string
    errCodes            : ErroCode list
    localVariables      : LocalVariable list
}

type AcnFunction = {
    funcName            : string option               // the name of the function. Valid only for TASes)
    func                : string option               // the body of the function
    funcDef             : string option               // function definition
    funcBody            : FuncParamType -> (AcnFuncBodyResult option)            // returns a list of validations statements
}

type Integer = {
    //bast inherrited properties
    baseInfo             : Asn1AcnAst.Integer

    //DAst properties
    //baseTypeEquivalence: BaseTypesEquivalence<Integer>

    typeDefinition      : TypeDefinitionCommon
    printValue          : (Asn1ValueKind option) -> (Asn1ValueKind) -> string
    initialValue        : IntegerValue
    initFunction        : InitFunction
    equalFunction       : EqualFunction
    isValidFunction     : IsValidFunction option      // it is optional because some types do not require an IsValid function (e.g. an unconstraint integer)
    uperEncFunction     : UPerFunction
    uperDecFunction     : UPerFunction
    acnEncFunction      : AcnFunction
    acnDecFunction      : AcnFunction
    

}

type Enumerated = {
    baseInfo             : Asn1AcnAst.Enumerated

    //DAst properties
    //baseTypeEquivalence: BaseTypesEquivalence<Enumerated>
    typeDefinition      : TypeDefinitionCommon
    printValue          : (Asn1ValueKind option) -> (Asn1ValueKind) -> string
    initialValue        : EnumValue
    initFunction        : InitFunction
    equalFunction       : EqualFunction
    isValidFunction     : IsValidFunction option      // it is optional because some types do not require an IsValid function (e.g. an unconstraint integer)
    uperEncFunction     : UPerFunction
    uperDecFunction     : UPerFunction
}

type Real = {
    baseInfo             : Asn1AcnAst.Real

    //DAst properties
    //baseTypeEquivalence: BaseTypesEquivalence<Real>
    typeDefinition      : TypeDefinitionCommon
    printValue          : (Asn1ValueKind option) -> (Asn1ValueKind) -> string
    initialValue        : RealValue
    initFunction        : InitFunction
    equalFunction       : EqualFunction
    isValidFunction     : IsValidFunction option      // it is optional because some types do not require an IsValid function (e.g. an unconstraint integer)
    uperEncFunction     : UPerFunction
    uperDecFunction     : UPerFunction
}


type Boolean = {
    baseInfo             : Asn1AcnAst.Boolean

    //DAst properties
    //baseTypeEquivalence: BaseTypesEquivalence<Boolean>
    typeDefinition      : TypeDefinitionCommon
    printValue          : (Asn1ValueKind option) -> (Asn1ValueKind) -> string
    initialValue        : BooleanValue
    initFunction        : InitFunction
    equalFunction       : EqualFunction
    isValidFunction     : IsValidFunction option      // it is optional because some types do not require an IsValid function (e.g. an unconstraint integer)
    uperEncFunction     : UPerFunction
    uperDecFunction     : UPerFunction
    acnEncFunction      : AcnFunction
    acnDecFunction      : AcnFunction
}


type NullType = {
    baseInfo             : Asn1AcnAst.NullType

    //DAst properties
    //baseTypeEquivalence: BaseTypesEquivalence<NullType>
    typeDefinition      : TypeDefinitionCommon
    printValue          : (Asn1ValueKind option) -> (Asn1ValueKind) -> string
    initFunction        : InitFunction
    initialValue        : NullValue
    equalFunction       : EqualFunction
    uperEncFunction     : UPerFunction
    uperDecFunction     : UPerFunction

}


type StringType = {
    baseInfo             : Asn1AcnAst.StringType

    //DAst properties
    //baseTypeEquivalence: BaseTypesEquivalence<StringType>
    typeDefinition      : TypeDefinitionCommon
    printValue          : (Asn1ValueKind option) -> (Asn1ValueKind) -> string
    initialValue        :  StringValue
    initFunction        : InitFunction
    equalFunction       : EqualFunction
    isValidFunction     : IsValidFunction option      // it is optional because some types do not require an IsValid function (e.g. an unconstraint integer)
    uperEncFunction     : UPerFunction
    uperDecFunction     : UPerFunction

}


type OctetString = {
    baseInfo             : Asn1AcnAst.OctetString


    //DAst properties
    //baseTypeEquivalence: BaseTypesEquivalence<OctetString>
    typeDefinition      : TypeDefinitionCommon
    printValue          : (Asn1ValueKind option) -> (Asn1ValueKind) -> string
    initialValue        : OctetStringValue
    initFunction        : InitFunction
    equalFunction       : EqualFunction
    isValidFunction     : IsValidFunction option      // it is optional because some types do not require an IsValid function (e.g. an unconstraint integer)
    uperEncFunction     : UPerFunction
    uperDecFunction     : UPerFunction

}



type BitString = {
    baseInfo             : Asn1AcnAst.BitString

    //DAst properties
    //baseTypeEquivalence: BaseTypesEquivalence<BitString>
    typeDefinition      : TypeDefinitionCommon
    printValue          : (Asn1ValueKind option) -> (Asn1ValueKind) -> string
    initialValue        : BitStringValue
    initFunction        : InitFunction
    equalFunction       : EqualFunction
    isValidFunction     : IsValidFunction option      // it is optional because some types do not require an IsValid function (e.g. an unconstraint integer)
    uperEncFunction     : UPerFunction
    uperDecFunction     : UPerFunction

}


type SequenceOf = {
    baseInfo            : Asn1AcnAst.SequenceOf
    childType           : Asn1Type

    //DAst properties
    //baseTypeEquivalence: BaseTypesEquivalence<SequenceOf>
    typeDefinition      : TypeDefinitionCommon
    printValue          : (Asn1ValueKind option) -> (Asn1ValueKind) -> string
    initialValue        : SeqOfValue
    initFunction        : InitFunction
    equalFunction       : EqualFunction
    isValidFunction     : IsValidFunction option      
    uperEncFunction     : UPerFunction
    uperDecFunction     : UPerFunction

}


and SeqChoiceChildInfoIsValid = {
    isValidStatement  : string -> string -> string
    localVars         : LocalVariable list
    alphaFuncs        : AlphaFunc list
    errCode           : ErroCode list
}

(*
and SeqChildInfo = {
    baseInfo            : Asn1AcnAst.SeqChildInfo
    chType              : Asn1Type
    

    //DAst properties
    c_name              : string
    isEqualBodyStats    : string -> string  -> string -> (string*(LocalVariable list)) option  // 
    isValidBodyStats    : int -> (SeqChoiceChildInfoIsValid option * int)
}

*)

and AcnChild = {
    Name                        : StringLoc
    id                          : ReferenceToType
    Type                        : Asn1AcnAst.AcnInsertedType
    typeDefinitionBodyWithinSeq : string
}

and SeqChildInfo = 
    | Asn1Child of Asn1Child
    | AcnChild  of AcnChild


and Asn1Child = {
    Name                        : StringLoc
    c_name                      : string
    ada_name                    : string                     
    isEqualBodyStats            : string -> string  -> string -> (string*(LocalVariable list)) option  // 
    isValidBodyStats            : int -> (SeqChoiceChildInfoIsValid option * int)
    Type                        : Asn1Type
    Optionality                 : Asn1AcnAst.Asn1Optionality option
    Comments                    : string array
}



and Sequence = {
    baseInfo            : Asn1AcnAst.Sequence
    children            : SeqChildInfo list


    //DAst properties
    //baseTypeEquivalence: BaseTypesEquivalence<Sequence>
    typeDefinition      : TypeDefinitionCommon
    printValue          : (Asn1ValueKind option) -> (Asn1ValueKind) -> string
    initialValue        : SeqValue
    initFunction        : InitFunction
    equalFunction       : EqualFunction
    isValidFunction     : IsValidFunction option      // it is optional because some types do not require an IsValid function (e.g. an unconstraint integer)
    uperEncFunction     : UPerFunction
    uperDecFunction     : UPerFunction
    //
    acnEncFunction      : AcnFunction
    acnDecFunction      : AcnFunction
}




and ChChildInfo = {
    Name                        : StringLoc
    c_name                      : string
    ada_name                    : string                     
    present_when_name           : string // Does not contain the "_PRESENT". Not to be used directly by backends.
    acnPresentWhenConditions    : Asn1AcnAst.AcnPresentWhenConditionChoiceChild list
    Comments                    : string array

    chType              :Asn1Type
    
    //DAst properties
    isEqualBodyStats    : string -> string -> string -> string*(LocalVariable list) // 
    isValidBodyStats    : int -> (SeqChoiceChildInfoIsValid option * int)
}

and Choice = {
    baseInfo            : Asn1AcnAst.Choice
    children            : ChChildInfo list

    //DAst properties
    //baseTypeEquivalence: BaseTypesEquivalence<Choice>
    typeDefinition      : TypeDefinitionCommon
    printValue          : (Asn1ValueKind option) -> (Asn1ValueKind) -> string
    initialValue        : ChValue
    initFunction        : InitFunction
    equalFunction       : EqualFunction
    isValidFunction     : IsValidFunction option      
    uperEncFunction     : UPerFunction
    uperDecFunction     : UPerFunction

}


and ReferenceType = {
    baseInfo            : Asn1AcnAst.ReferenceType
    baseType            : Asn1Type

    typeDefinition      : TypeDefinitionCommon
    printValue          : (Asn1ValueKind option) -> (Asn1ValueKind) -> string
    initialValue        : Asn1Value
    initFunction        : InitFunction
    equalFunction       : EqualFunction
    isValidFunction     : IsValidFunction option      
    uperEncFunction     : UPerFunction
    uperDecFunction     : UPerFunction

}

and Asn1Type = {
    id              : ReferenceToType
    acnAligment     : Asn1AcnAst.AcnAligment option
    acnParameters   : Asn1AcnAst.AcnParameter list
    Location        : SrcLoc //Line no, Char pos
    tasInfo         : TypeAssignmentInfo option
    Kind            : Asn1TypeKind
}


and Asn1TypeKind =
    | Integer           of Integer
    | Real              of Real
    | IA5String         of StringType
    | OctetString       of OctetString
    | NullType          of NullType
    | BitString         of BitString
    | Boolean           of Boolean
    | Enumerated        of Enumerated
    | SequenceOf        of SequenceOf
    | Sequence          of Sequence
    | Choice            of Choice
    | ReferenceType     of ReferenceType

type State = {
    curSeqOfLevel : int
    currentTypes  : Asn1Type list
    currErrCode   : int

}


type TypeAssignment = {
    Name:StringLoc
    c_name:string
    ada_name:string
    Type:Asn1Type
    Comments: string array

}

type ValueAssignment = {
    Name    :StringLoc
    c_name  :string
    ada_name:string
    Type    :Asn1Type
    Value   :Asn1Value
}




type Asn1Module = {
    Name : StringLoc
    TypeAssignments : list<TypeAssignment>
    ValueAssignments : list<ValueAssignment>
    Imports : list<Asn1Ast.ImportedModule>
    Exports : Asn1Ast.Exports
    Comments : string array
}

type Asn1File = {
    FileName:string;
    Tokens: IToken array
    Modules : list<Asn1Module>
}

type ProgramUnit = {
    name    : string
    specFileName            : string
    bodyFileName            : string
    sortedTypeAssignments   : TypeAssignment list
    valueAssignments        : ValueAssignment list
    importedProgramUnits    : string list
}


type AstRoot = {
    Files: Asn1File list
    acnConstants : Map<string, BigInteger>
    args:CommandLineSettings
    programUnits : ProgramUnit list
    lang         : ProgrammingLanguage
}

(*
type AstRoot = {
    Files                   : Asn1File list
    args                    : CommandLineSettings
    //valsMap                 : Map<ReferenceToValue, Asn1GenericValue>
    typesMap                : Map<ReferenceToType, Asn1Type>
    TypeAssignments         : Asn1Type list
    ValueAssignments        : Asn1GenericValue list
    programUnits            : ProgramUnit list
    acnConstants            : Map<string, BigInteger>
    acnLinks                : AcnLink list
}
*)


