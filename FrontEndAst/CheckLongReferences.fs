﻿module CheckLongReferences


open System
open System.Numerics
open System.IO
open System.Xml.Linq
open FsUtils
open CommonTypes
open Asn1AcnAst
open Asn1Fold
open Asn1AcnAstUtilFunctions



let private addParameter (cur:AcnInsertedFieldDependencies) (refType:ReferenceToType, p:AcnParameter) (d:DependencyKind) =
    let newKey = 
        match refType with
        | ReferenceToType nodes ->ReferenceToType(nodes@[PRM p.name])
    match cur.acnFields.TryFind newKey with
    | None      -> {cur with acnFields = cur.acnFields.Add(newKey,[d])}
    | Some lst  -> {cur with acnFields = cur.acnFields.Add(newKey,lst@[d])}

let private addAcnChild (cur:AcnInsertedFieldDependencies) (acnRefId:ReferenceToType) (d:DependencyKind) =
    let newKey = acnRefId
    match cur.acnFields.TryFind newKey with
    | None      -> {cur with acnFields = cur.acnFields.Add(newKey,[d])}
    | Some lst  -> {cur with acnFields = cur.acnFields.Add(newKey,lst@[d])}

let private checkRelativePath (curState:AcnInsertedFieldDependencies) (parents: Asn1Type list) (visibleParameters:(ReferenceToType*AcnParameter) list) (RelativePath path : RelativePath) (d:DependencyKind) checkParameter checkAcnType =
    match path with
    | []        -> raise (BugErrorException("Invalid Argument"))
    | x1::[]    ->
        match visibleParameters |> Seq.tryFind(fun (ref, p) -> p.name = x1.Value) with
        | Some  (rf,p)   -> 
            //x1 is a parameter
            checkParameter p
            addParameter curState (rf,p) d
        | None      -> 
            //x1 is silbing child
            match parents |> List.rev with
            | []    -> raise(SemanticError(x1.Location, (sprintf "Invalid reference '%s'" x1.Value)))
            | parent::_ ->
                match parent.Kind with
                | Sequence seq ->
                    match seq.children |> Seq.tryFind(fun (c:SeqChildInfo) -> c.Name = x1) with
                    | Some ch   -> 
                        match ch with
                        | Asn1Child ch  -> raise(SemanticError(x1.Location, (sprintf "Invalid reference '%s' to ASN.1 type. ACN references must point to ACN inserted types or parameters" x1.Value)))
                        | AcnChild ch  -> 
                            checkAcnType ch
                            addAcnChild curState ch.id d
                    | None      -> raise(SemanticError(x1.Location, (sprintf "Invalid reference '%s'" x1.Value)))
                | _                 -> raise(SemanticError(x1.Location, (sprintf "Invalid reference '%s'" x1.Value)))
    | x1::_  -> 
            //x1 may be a silbing child or a child to one of my parents
            // so, find the first parent that has a child x1

        let parSeq = 
            let rec findSeq p =
                match p.Kind with
                | Sequence seq      -> seq.children |> Seq.exists(fun c -> c.Name = x1) 
                | ReferenceType ref ->findSeq ref.baseType
                | _            -> raise(SemanticError(x1.Location, (sprintf "Invalid reference '%s'" (path |> Seq.StrJoin "."))))
            parents |> List.rev |>  Seq.tryFind findSeq
        let rec checkSeqWithPath (seq:Asn1Type) (path : StringLoc list)=   
            match seq.Kind with
            | Sequence seq -> 
                match path with
                | []       -> raise(BugErrorException "111")
                | x1::[]   ->
                    match seq.children |> Seq.tryFind(fun c -> c.Name = x1) with
                    | None -> raise(SemanticError(x1.Location, (sprintf "Invalid reference '%s'" (path |> Seq.StrJoin "."))))
                    | Some ch -> 
                        match ch with
                        | Asn1Child ch  -> raise(SemanticError(x1.Location, (sprintf "Invalid reference '%s' to ASN.1 type. ACN references must point to ACN inserted types or parameters" x1.Value)))
                        | AcnChild ch  -> 
                            checkAcnType ch
                            addAcnChild curState ch.id d
                | x1::xs     ->
                    match seq.children |> Seq.tryFind(fun c -> c.Name = x1) with
                    | None -> raise(SemanticError(x1.Location, (sprintf "Invalid reference '%s'" (path |> Seq.StrJoin "."))))
                    | Some ch -> 
                        match ch with
                        | Asn1Child ch -> checkSeqWithPath ch.Type xs
                        | _            -> raise(SemanticError(x1.Location, (sprintf "Invalid reference '%s'" (path |> Seq.StrJoin "."))))
            | ReferenceType ref -> checkSeqWithPath ref.baseType path
            | _            -> raise(SemanticError(x1.Location, (sprintf "Invalid reference '%s'" (path |> Seq.StrJoin "."))))

        match parSeq with
        | None  -> raise(SemanticError(x1.Location, (sprintf "Invalid reference '%s'" (path |> Seq.StrJoin "."))))
        | Some p -> 
            checkSeqWithPath p path

let sizeReference (curState:AcnInsertedFieldDependencies) (parents: Asn1Type list) (visibleParameters:(ReferenceToType*AcnParameter) list)  (rp :RelativePath  option) (d:DependencyKind) =
    match rp with
    | None      -> curState
    | Some (RelativePath path) -> 
        let loc = path.Head.Location
        let checkParameter (p:AcnParameter) = 
            match p.asn1Type with
            | AcnPrmInteger _ -> ()
            | _              -> raise(SemanticError(loc, (sprintf "Invalid argument type. Expecting INTEGER got %s "  (p.asn1Type.ToString()))))
        let checkAcnType (c:AcnChild) =
            match c.Type with
            | AcnInteger    _ -> ()
            | _              -> raise(SemanticError(loc, (sprintf "Invalid argument type. Expecting INTEGER got %s "  (c.Type.AsString))))
        checkRelativePath curState parents visibleParameters   (RelativePath path) d checkParameter checkAcnType

let rec private checkType (parents: Asn1Type list) (curentPath : ScopeNode list) (t:Asn1Type) (curState:AcnInsertedFieldDependencies) = 
    //the visible parameters of a type are this type parameters if type has parameters or the 
    //next parent with parameters
    let visibleParameters = 
        match parents@[t] |> List.rev |> List.filter(fun x -> not x.acnParameters.IsEmpty) with
        | []    -> []
        | p1::_ -> p1.acnParameters |> List.map(fun p -> (p1.id, p))
    match t.Kind with
    | Integer        _
    | Real           _
    | NullType       _
    | Boolean        _
    | Enumerated     _      -> curState
    | IA5String      a
    | NumericString  a      ->
        match a.acnProperties.sizeProp with
        | Some (StrExternalField   relPath)    ->
            sizeReference curState parents visibleParameters (Some relPath) (DepIA5StringSizeDeterminant t.id)
        | _          -> curState
    | OctetString    a      -> 
        sizeReference curState parents visibleParameters a.acnProperties.sizeProp (DepOtherSizeDeterminant t.id)
    | BitString      a      -> 
        sizeReference curState parents visibleParameters a.acnProperties.sizeProp (DepOtherSizeDeterminant t.id)
    | SequenceOf   seqOf    ->
        let ns = sizeReference curState (parents@[t]) visibleParameters seqOf.acnProperties.sizeProp (DepOtherSizeDeterminant t.id)
        checkType (parents@[t]) (curentPath@[SQF]) seqOf.child ns
    | Sequence   seq        ->
        seq.children |>
        List.choose (fun c -> match c with Asn1Child ac -> Some ac | AcnChild _ -> None) |>
        List.fold (fun ns ac -> 
            let ns1 = 
                match ac.Optionality with
                | Some (Optional opt)   -> 
                    match opt.acnPresentWhen with
                    | None  -> ns
                    | Some   (PresenceWhenBool (RelativePath path)) ->
                        let loc = path.Head.Location
                        let checkParameter (p:AcnParameter) = 
                            match p.asn1Type with
                            | AcnPrmBoolean _ -> ()
                            | _              -> raise(SemanticError(loc, (sprintf "Invalid argument type. Expecting BOOLEAN got %s "  (p.asn1Type.ToString()))))
                        let rec checkAcnType (c:AcnChild) =
                            match c.Type with
                            | AcnBoolean    _ -> ()
                            | _              -> raise(SemanticError(loc, (sprintf "Invalid argument type. Expecting BOOLEAN got %s "  (c.Type.AsString))))
                        checkRelativePath ns (parents@[t]) visibleParameters   (RelativePath path) (DepPresenceBool ac.Type.id) checkParameter checkAcnType 
                | _                     -> ns
            checkType (parents@[t]) (curentPath@[SEQ_CHILD ac.Name.Value])  ac.Type ns1
        ) curState
    | Choice ch ->
        ch.children |>
        List.fold (fun ns ch -> 
            let checkAcnPresentWhenConditionChoiceChild (curState:AcnInsertedFieldDependencies) (a:AcnPresentWhenConditionChoiceChild) =
                match a with
                | PresenceInt   ((RelativePath path),_) -> 
                    let loc = path.Head.Location
                    let checkParameter (p:AcnParameter) = 
                        match p.asn1Type with
                        | AcnPrmInteger _ -> ()
                        | _              -> raise(SemanticError(loc, (sprintf "Invalid argument type. Expecting INTEGER got %s "  (p.asn1Type.ToString()))))
                    let rec checkAcnType (c:AcnChild) =
                        match c.Type with
                        | AcnInteger    _ -> ()
                        | _              -> raise(SemanticError(loc, (sprintf "Invalid argument type. Expecting INTEGER got %s "  (c.Type.AsString))))
                    checkRelativePath curState (parents@[t]) visibleParameters   (RelativePath path) (DepPresenceInt ch.Type.id) checkParameter checkAcnType
                | PresenceStr   ((RelativePath path),_ )-> 
                    let loc = path.Head.Location
                    let checkParameter (p:AcnParameter) = 
                        match p.asn1Type with
                        | AcnPrmRefType _ -> ()
                        | _              -> raise(SemanticError(loc, (sprintf "Invalid argument type. Expecting STRING got %s "  (p.asn1Type.ToString()))))
                    let rec checkAcnType (c:AcnChild) =
                        match c.Type with
                        | _              -> raise(SemanticError(loc, (sprintf "Invalid argument type. Expecting STRING got %s "  (c.Type.AsString))))
                    checkRelativePath curState (parents@[t]) visibleParameters   (RelativePath path) (DepPresenceStr ch.Type.id) checkParameter checkAcnType
            
            let ns1 = ch.acnPresentWhenConditions |> List.fold (fun ns x -> checkAcnPresentWhenConditionChoiceChild ns x) ns
            checkType (parents@[t]) (curentPath@[CH_CHILD ch.Name.Value])  ch.Type ns1) curState
    | ReferenceType ref -> 
        let checkArgument (curState:AcnInsertedFieldDependencies) ((RelativePath path : RelativePath), (prm:AcnParameter)) =
            let loc = path.Head.Location
            let checkParameter (p:AcnParameter) = 
                match p.asn1Type = prm.asn1Type with
                | true  -> ()
                | false -> raise(SemanticError(loc, (sprintf "Invalid argument type. Expecting %s got %s " (prm.asn1Type.ToString()) (p.asn1Type.ToString()))))
            let rec checkAcnType (c:AcnChild) =
                match c.Type, prm.asn1Type with
                | AcnInteger    _, AcnPrmInteger _   -> ()
                | AcnNullType   _, AcnPrmNullType _  -> ()
                | AcnBoolean    _, AcnPrmBoolean _   -> ()
                | _             , AcnPrmRefType (md,ts)    -> ()    //WARNING NOT CHECKED
                | _                                  -> raise(SemanticError(loc, (sprintf "Invalid argument type. Expecting %s got %s " (prm.asn1Type.ToString()) (c.Type.AsString))))
            checkRelativePath curState parents visibleParameters   (RelativePath path) (DepRefTypeArgument t.id) checkParameter checkAcnType

        match ref.acnArguments.Length = ref.baseType.acnParameters.Length with
        | true  -> ()
        | false -> raise(SemanticError(t.Location, (sprintf "Expecting %d arguments, provide %d" ref.acnArguments.Length ref.baseType.acnParameters.Length)))
        let ziped = List.zip ref.acnArguments ref.baseType.acnParameters
        let ns = ziped |> List.fold(fun s c -> checkArgument s c) curState
        checkType parents curentPath ref.baseType ns


let checkAst (r:AstRoot) =
    let emptyState = {AcnInsertedFieldDependencies.acnFields = Map.empty}

    r.Files |>
        List.collect (fun f -> f.Modules) |>
        List.map (fun m -> m.TypeAssignments |> List.map(fun tas -> (m,tas)) ) |>
        List.collect id |>
        List.fold (fun ns (m,tas) -> checkType [] [MD m.Name.Value; TA tas.Name.Value] tas.Type ns) emptyState


