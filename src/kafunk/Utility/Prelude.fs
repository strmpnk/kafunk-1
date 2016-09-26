namespace Kafunk

[<AutoOpen>]
module Prelude =

  /// Determines whether the argument is a null reference.
  let inline isNull a = obj.ReferenceEquals(null, a)

  /// Given a value, creates a function with one ignored argument which returns the value.
  let inline konst x _ = x

  /// Active pattern for matching Result<'a, 'e>.
  let (|Success|Failure|) = function | Choice1Of2 a -> Success a | Choice2Of2 b -> Failure b

  let flip f a b = f b a


module Choice =

  let fold (f:'a -> 'c) (g:'b -> 'c) (c:Choice<'a, 'b>) : 'c =
    match c with
    | Choice1Of2 a -> f a
    | Choice2Of2 b -> g b

  let mapLeft (f:'a -> 'c) (c:Choice<'a, 'b>) : Choice<'c, 'b> =
    match c with
    | Choice1Of2 a -> Choice1Of2 (f a)
    | Choice2Of2 b -> Choice2Of2 b

  let mapRight (f:'b -> 'c) (c:Choice<'a, 'b>) : Choice<'a, 'c> =
    match c with
    | Choice1Of2 a -> Choice1Of2 a
    | Choice2Of2 b -> Choice2Of2 (f b)





// pooling

open System.Collections.Concurrent

type ObjectPool<'a>(initial:int, create:unit -> 'a) =

  let pool = new ConcurrentStack<'a>(Seq.init initial (fun _ -> create()))

  member x.Push(a:'a) =
    pool.Push(a)

  member x.Pop() =
    let mutable a = Unchecked.defaultof<'a>
    if not (pool.TryPop(&a)) then
      failwith "out of sockets!"
      //create ()
    else a



// log

// TODO: implement!

type LogLevel =
  | Trace | Info | Warn | Error | Fatal
  with
    override x.ToString () =
      match x with
      | Trace -> "TRACE" | Info -> "INFO" | Warn -> "WARN" | Error -> "ERROR" | Fatal -> "FATAL"

type Logger = {
  name : string
}


[<AutoOpen>]
module LoggerEx =
  open System

  type Logger with

    member inline ts.log (format, level:LogLevel) =
      let inline trace (m:string) =
        Console.WriteLine(String.Format("{0:yyyy-MM-dd hh:mm:ss:ffff}|{1}|{2}|{3}", DateTime.Now, (level.ToString()), ts.name, m))
      Printf.kprintf trace format

    member inline ts.fatal format = ts.log (format, LogLevel.Fatal)

    member inline ts.error format = ts.log (format, LogLevel.Error)

    member inline ts.warn format = ts.log (format, LogLevel.Warn)

    member inline ts.info format = ts.log (format, LogLevel.Info)

    member inline ts.trace format = ts.log (format, LogLevel.Trace)

[<RequireQualifiedAccess>]
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Log =

  let create name = { name = name }




// collection helpers

// collection helpers

open System.Collections.Generic

/// Basic operations on dictionaries.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Dict =

  let tryGet k (d:#IDictionary<_,_>) =
    let mutable v = Unchecked.defaultof<_>
    if d.TryGetValue(k, &v) then Some v
    else None


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Array =

  let inline item i a = Array.get a i

  let inline singleton a = [|a|]

  /// Partitions the array into the specified number of groups.
  /// If there are more groups than elements, then the empty groups are returned.
  /// When the array doesn't divide into the number of groups, the last group will
  /// have one fewer element.
  let groupInto (groups:int) (a:'a[]) : 'a[][] =
    if groups < 1 then invalidArg "groups" "must be positive"
    let perGroup = int (ceil (float a.Length / float groups))
    let groups = Array.zeroCreate groups
    for i = 0 to groups.Length - 1 do
      let group = ResizeArray<_>(perGroup)
      for j = 0 to perGroup - 1 do
        let idx = i * perGroup + j
        if idx < a.Length then
          group.Add (a.[idx])
      groups.[i] <- group.ToArray()
    groups

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Map =

  let addMany (kvps:('a * 'b) seq) (m:Map<'a, 'b>) : Map<'a, 'b> =
    kvps |> Seq.fold (fun m (k,v) -> Map.add k v m) m

// --------------------------------------------------------------------------------------------------


type Result<'a, 'e> = Choice<'a, 'e>

[<AutoOpen>]
module ResultEx =

  let (|Success|Failure|) (c:Result<'a, 'e>) =
    match c with
    | Choice1Of2 a -> Success a
    | Choice2Of2 e -> Failure e

  let Success a : Result<'a, 'e> = Choice1Of2 a

  let Failure e : Result<'a, 'e> = Choice2Of2 e

module Result =

  let success a : Result<'a, 'e> = Choice1Of2 a

  let fail e : Result<'a, 'e> = Choice2Of2 e

  let inline map f (r:Result<'a, 'e>) : Result<'b, 'e> =
    Choice.mapLeft f r

  let inline mapError f (r:Result<'a, 'e>) : Result<'a, 'e2> =
     Choice.mapRight f r

  let bind (f:'a -> Result<'b, 'e>) (r:Result<'a, 'e>) : Result<'b, 'e> =
    match r with
    | Choice1Of2 a -> f a
    | Choice2Of2 e -> Choice2Of2 e

  let join (c:Result<Result<'a, 'e>, 'e>) : Result<'a, 'e> =
    match c with
    | Choice1Of2 (Choice1Of2 a) -> Choice1Of2 a
    | Choice1Of2 (Choice2Of2 e) | Choice2Of2 e -> Choice2Of2 e

  let fold f g (r:Result<'a, 'e>) : 'b =
    Choice.fold f g r

  let traverse (f:'a -> Result<'b, 'e>) (xs:seq<'a>) : Result<'b[], 'e> =
    use en = xs.GetEnumerator()
    let oks = ResizeArray<_>()
    let err = ref None
    while en.MoveNext() && err.Value.IsNone do
      match f en.Current with
      | Choice1Of2 b -> oks.Add b
      | Choice2Of2 e -> err := Some e
    match !err with
    | Some e -> Choice2Of2 e
    | None -> Choice1Of2 (oks.ToArray())