// ============================================================================
// RailwayForge Formal Specification & Machine-Checked Proofs (Dafny 4.11)
//
// Proves the core algebraic, memory-state, and pipeline safety laws of
// RailwayForge's Errors-as-Values Result<T, E> combinators:
//   1. Disjointness & totality (never simultaneously Ok and Err)
//   2. Functor identity & composition laws for Map and MapError
//   3. Monad left identity, right identity, and associativity laws for Bind
//   4. Short-circuit error preservation across Map, Bind, Ensure, OrElse
//   5. Guard soundness for Ensure and totality for Recover
//   6. Multi-error accumulation completeness for Combine and Collect
//   7. Three-state [Union] struct safety: Uninitialized state is unreachable
//   8. Partition conservation law: |Values| + |Errors| == |Inputs|
//   9. Traverse fail-fast first-error soundness
//  10. ValidationBag monotonicity & completeness
//  11. Append-only Railway execution history invariants
// ============================================================================

module RailwayLaws {

  datatype Result<T, E> = Ok(value: T) | Err(error: E)

  function Map<T, U, E>(r: Result<T, E>, f: T -> U): Result<U, E> {
    match r
    case Ok(v) => Ok(f(v))
    case Err(e) => Err(e)
  }

  function MapError<T, E1, E2>(r: Result<T, E1>, f: E1 -> E2): Result<T, E2> {
    match r
    case Ok(v) => Ok(v)
    case Err(e) => Err(f(e))
  }

  function Bind<T, U, E>(r: Result<T, E>, f: T -> Result<U, E>): Result<U, E> {
    match r
    case Ok(v) => f(v)
    case Err(e) => Err(e)
  }

  function Ensure<T, E>(r: Result<T, E>, pred: T -> bool, mkErr: T -> E): Result<T, E> {
    match r
    case Ok(v) => if pred(v) then Ok(v) else Err(mkErr(v))
    case Err(e) => Err(e)
  }

  function Recover<T, E>(r: Result<T, E>, fallback: E -> T): T {
    match r
    case Ok(v) => v
    case Err(e) => fallback(e)
  }

  function OrElse<T, E1, E2>(r: Result<T, E1>, recover: E1 -> Result<T, E2>): Result<T, E2> {
    match r
    case Ok(v) => Ok(v)
    case Err(e) => recover(e)
  }

  function Combine<T1, T2, E>(r1: Result<T1, E>, r2: Result<T2, E>): Result<(T1, T2), seq<E>> {
    match (r1, r2)
    case (Ok(v1), Ok(v2)) => Ok((v1, v2))
    case (Err(e1), Ok(_)) => Err([e1])
    case (Ok(_), Err(e2)) => Err([e2])
    case (Err(e1), Err(e2)) => Err([e1, e2])
  }

  function CollectErrors<T, E>(rs: seq<Result<T, E>>): seq<E> {
    if |rs| == 0 then []
    else match rs[0]
      case Ok(_) => CollectErrors(rs[1..])
      case Err(e) => [e] + CollectErrors(rs[1..])
  }

  function CollectValues<T, E>(rs: seq<Result<T, E>>): seq<T>
    requires forall i :: 0 <= i < |rs| ==> rs[i].Ok?
    ensures |CollectValues(rs)| == |rs|
    ensures forall i :: 0 <= i < |rs| ==> CollectValues(rs)[i] == rs[i].value
  {
    if |rs| == 0 then []
    else [rs[0].value] + CollectValues(rs[1..])
  }

  function Collect<T, E>(rs: seq<Result<T, E>>): Result<seq<T>, seq<E>> {
    var errs := CollectErrors(rs);
    if |errs| == 0 then
      assert forall i :: 0 <= i < |rs| ==> rs[i].Ok? by {
        LemmaNoErrorsMeansAllOk(rs);
      }
      Ok(CollectValues(rs))
    else
      Err(errs)
  }

  // ---- 1. Disjointness & Totality ----

  lemma Disjointness<T, E>(r: Result<T, E>)
    ensures r.Ok? <==> !r.Err?
  {}

  // ---- 2. Functor Laws ----

  lemma MapIdentity<T, E>(r: Result<T, E>)
    ensures Map(r, (x: T) => x) == r
  {}

  lemma MapComposition<T, U, V, E>(r: Result<T, E>, f: T -> U, g: U -> V)
    ensures Map(Map(r, f), g) == Map(r, (x: T) => g(f(x)))
  {}

  lemma MapErrorIdentity<T, E>(r: Result<T, E>)
    ensures MapError(r, (e: E) => e) == r
  {}

  lemma MapErrorComposition<T, E1, E2, E3>(r: Result<T, E1>, f: E1 -> E2, g: E2 -> E3)
    ensures MapError(MapError(r, f), g) == MapError(r, (e: E1) => g(f(e)))
  {}

  // ---- 3. Monad Laws ----

  lemma MonadLeftIdentity<T, U, E>(x: T, f: T -> Result<U, E>)
    ensures Bind(Ok(x), f) == f(x)
  {}

  lemma MonadRightIdentity<T, E>(r: Result<T, E>)
    ensures Bind(r, (x: T) => Ok(x)) == r
  {}

  lemma MonadAssociativity<T, U, V, E>(
    r: Result<T, E>,
    f: T -> Result<U, E>,
    g: U -> Result<V, E>
  )
    ensures Bind(Bind(r, f), g) == Bind(r, (x: T) => Bind(f(x), g))
  {}

  // ---- 4. Short-Circuit Error Preservation ----

  lemma ShortCircuitPreservesError<T, U, E>(
    e: E,
    f: T -> U,
    g: T -> Result<U, E>,
    p: T -> bool,
    mkErr: T -> E
  )
    ensures Map(Err(e), f) == Err(e)
    ensures Bind(Err(e), g) == Err(e)
    ensures Ensure(Err(e), p, mkErr) == Err(e)
  {}

  // ---- 5. Guard & Recovery Soundness ----

  lemma EnsureSoundness<T, E>(r: Result<T, E>, pred: T -> bool, mkErr: T -> E)
    ensures Ensure(r, pred, mkErr).Ok? <==> (r.Ok? && pred(r.value))
    ensures r.Err? ==> Ensure(r, pred, mkErr) == r
  {}

  lemma RecoverTotality<T, E>(r: Result<T, E>, fallback: E -> T)
    ensures r.Ok? ==> Recover(r, fallback) == r.value
    ensures r.Err? ==> Recover(r, fallback) == fallback(r.error)
  {}

  lemma OrElsePreservesOk<T, E1, E2>(v: T, recover: E1 -> Result<T, E2>)
    ensures OrElse(Ok(v), recover) == Ok(v)
  {}

  // ---- 6. Multi-Error Accumulation Soundness ----

  lemma CombineSoundness<T1, T2, E>(r1: Result<T1, E>, r2: Result<T2, E>)
    ensures Combine(r1, r2).Ok? <==> (r1.Ok? && r2.Ok?)
    ensures (r1.Err? && r2.Err?) ==> Combine(r1, r2) == Err([r1.error, r2.error])
    ensures (r1.Err? && r2.Ok?) ==> Combine(r1, r2) == Err([r1.error])
    ensures (r1.Ok? && r2.Err?) ==> Combine(r1, r2) == Err([r2.error])
  {}

  lemma LemmaNoErrorsMeansAllOk<T, E>(rs: seq<Result<T, E>>)
    requires |CollectErrors(rs)| == 0
    ensures forall i :: 0 <= i < |rs| ==> rs[i].Ok?
  {
    if |rs| > 0 {
      assert rs[0].Ok?;
      LemmaNoErrorsMeansAllOk(rs[1..]);
    }
  }

  lemma LemmaAnyErrorAppearsInCollectErrors<T, E>(rs: seq<Result<T, E>>, idx: int)
    requires 0 <= idx < |rs|
    requires rs[idx].Err?
    ensures rs[idx].error in CollectErrors(rs)
    ensures |CollectErrors(rs)| >= 1
  {
    if idx == 0 {
      assert CollectErrors(rs)[0] == rs[0].error;
    } else {
      LemmaAnyErrorAppearsInCollectErrors(rs[1..], idx - 1);
    }
  }

  lemma LemmaAllOkNoErrors<T, E>(rs: seq<Result<T, E>>)
    requires forall i :: 0 <= i < |rs| ==> rs[i].Ok?
    ensures |CollectErrors(rs)| == 0
  {
    if |rs| > 0 {
      LemmaAllOkNoErrors(rs[1..]);
    }
  }

  lemma CollectCompleteAndSound<T, E>(rs: seq<Result<T, E>>)
    ensures Collect(rs).Ok? <==> (forall i :: 0 <= i < |rs| ==> rs[i].Ok?)
    ensures Collect(rs).Ok? ==> |Collect(rs).value| == |rs|
    ensures Collect(rs).Err? ==> |Collect(rs).error| >= 1
    ensures forall i :: 0 <= i < |rs| && rs[i].Err? ==>
      Collect(rs).Err? && rs[i].error in Collect(rs).error
  {
    if forall i :: 0 <= i < |rs| ==> rs[i].Ok? {
      LemmaAllOkNoErrors(rs);
    } else {
      var k :| 0 <= k < |rs| && rs[k].Err?;
      LemmaAnyErrorAppearsInCollectErrors(rs, k);
    }
    forall i | 0 <= i < |rs| && rs[i].Err?
      ensures Collect(rs).Err? && rs[i].error in Collect(rs).error
    {
      LemmaAnyErrorAppearsInCollectErrors(rs, i);
    }
  }

  // ---- 7. Three-State [Union] Struct Safety (Uninitialized Unreachability) ----

  datatype StructState<T, E> = Uninitialized | StateOk(v: T) | StateErr(e: E)

  predicate IsInitialized<T, E>(s: StructState<T, E>) {
    !s.Uninitialized?
  }

  function StructMap<T, U, E>(s: StructState<T, E>, f: T -> U): StructState<U, E>
    requires IsInitialized(s)
  {
    match s
    case StateOk(v) => StateOk(f(v))
    case StateErr(e) => StateErr(e)
  }

  function StructBind<T(!new), U, E>(s: StructState<T, E>, f: T -> StructState<U, E>): StructState<U, E>
    requires IsInitialized(s)
    requires forall x: T :: IsInitialized(f(x))
  {
    match s
    case StateOk(v) => f(v)
    case StateErr(e) => StateErr(e)
  }

  lemma InitializedClosedUnderCombinators<T(!new), U, E>(
    s: StructState<T, E>,
    f: T -> U,
    g: T -> StructState<U, E>
  )
    requires IsInitialized(s)
    requires forall x: T :: IsInitialized(g(x))
    ensures IsInitialized(StructMap(s, f))
    ensures IsInitialized(StructBind(s, g))
  {}

  // ---- 8. Partition Conservation Law ----

  function PartitionValues<T, E>(rs: seq<Result<T, E>>): seq<T> {
    if |rs| == 0 then []
    else match rs[0]
      case Ok(v) => [v] + PartitionValues(rs[1..])
      case Err(_) => PartitionValues(rs[1..])
  }

  lemma PartitionConservationLaw<T, E>(rs: seq<Result<T, E>>)
    ensures |PartitionValues(rs)| + |CollectErrors(rs)| == |rs|
    ensures forall i :: 0 <= i < |rs| && rs[i].Ok? ==> rs[i].value in PartitionValues(rs)
    ensures forall i :: 0 <= i < |rs| && rs[i].Err? ==> rs[i].error in CollectErrors(rs)
  {
    if |rs| > 0 {
      PartitionConservationLaw(rs[1..]);
    }
  }

  // ---- 9. Traverse Fail-Fast First-Error Soundness ----

  function Traverse<TIn, TOut, E>(items: seq<TIn>, f: TIn -> Result<TOut, E>): Result<seq<TOut>, E> {
    if |items| == 0 then Ok([])
    else match f(items[0])
      case Err(e) => Err(e)
      case Ok(v) => match Traverse(items[1..], f)
        case Err(e) => Err(e)
        case Ok(rest) => Ok([v] + rest)
  }

  lemma TraverseAllOk<TIn, TOut, E>(items: seq<TIn>, f: TIn -> Result<TOut, E>)
    requires forall i :: 0 <= i < |items| ==> f(items[i]).Ok?
    ensures Traverse(items, f).Ok?
    ensures |Traverse(items, f).value| == |items|
    ensures forall i :: 0 <= i < |items| ==> Traverse(items, f).value[i] == f(items[i]).value
  {
    if |items| > 0 {
      TraverseAllOk(items[1..], f);
    }
  }

  lemma TraverseFailFastFirstError<TIn, TOut, E>(items: seq<TIn>, f: TIn -> Result<TOut, E>)
    requires exists i :: 0 <= i < |items| && f(items[i]).Err?
    ensures Traverse(items, f).Err?
    ensures exists k :: (
      0 <= k < |items| &&
      f(items[k]) == Err(Traverse(items, f).error) &&
      (forall j :: 0 <= j < k ==> f(items[j]).Ok?))
  {
    if f(items[0]).Err? {
      assert 0 < |items| && f(items[0]) == Err(Traverse(items, f).error);
    } else {
      var idx :| 0 <= idx < |items| && f(items[idx]).Err?;
      assert idx > 0;
      assert 0 <= idx - 1 < |items[1..]| && f(items[1..][idx - 1]).Err?;
      TraverseFailFastFirstError(items[1..], f);
      var k' :| 0 <= k' < |items[1..]|
        && f(items[1..][k']) == Err(Traverse(items[1..], f).error)
        && (forall j :: 0 <= j < k' ==> f(items[1..][j]).Ok?);
      var k := k' + 1;
      assert 0 <= k < |items|;
      assert f(items[k]) == Err(Traverse(items, f).error);
      assert forall j :: 0 <= j < k ==> f(items[j]).Ok?;
    }
  }

  // ---- 10. ValidationBag Monotonicity & Completeness ----

  function BagCheck<E>(bag: seq<E>, condition: bool, err: E): seq<E> {
    if condition then bag else bag + [err]
  }

  function BagBuild<T, E>(bag: seq<E>, value: T): Result<T, seq<E>> {
    if |bag| == 0 then Ok(value) else Err(bag)
  }

  lemma ValidationBagMonotonic<E>(bag: seq<E>, cond: bool, err: E)
    ensures |BagCheck(bag, cond, err)| >= |bag|
    ensures forall i :: 0 <= i < |bag| ==> BagCheck(bag, cond, err)[i] == bag[i]
    ensures !cond ==> err in BagCheck(bag, cond, err)
  {}

  lemma ValidationBagBuildSound<T, E>(bag: seq<E>, value: T)
    ensures BagBuild(bag, value).Ok? <==> |bag| == 0
    ensures BagBuild(bag, value).Err? ==> BagBuild(bag, value).error == bag
  {}

  // ---- 11. Append-Only Railway Execution History Invariants ----

  datatype StepOutcome = StepOk | StepFailed(code: int)

  datatype StepRecord = StepRecord(stepIndex: nat, outcome: StepOutcome)

  datatype RunTrace = RunTrace(steps: seq<StepRecord>, succeeded: bool)

  predicate ValidRunTrace(tr: RunTrace) {
    (|tr.steps| > 0) &&
    (tr.succeeded ==> forall i :: 0 <= i < |tr.steps| ==> tr.steps[i].outcome.StepOk?) &&
    (!tr.succeeded ==>
      tr.steps[|tr.steps| - 1].outcome.StepFailed? &&
      forall i :: 0 <= i < |tr.steps| - 1 ==> tr.steps[i].outcome.StepOk?)
  }

  function AppendHistory(history: seq<RunTrace>, entry: RunTrace): seq<RunTrace>
    requires ValidRunTrace(entry)
  {
    history + [entry]
  }

  lemma HistoryIsAppendOnlyAndValid(history: seq<RunTrace>, entry: RunTrace)
    requires forall i :: 0 <= i < |history| ==> ValidRunTrace(history[i])
    requires ValidRunTrace(entry)
    ensures var next := AppendHistory(history, entry);
      |next| == |history| + 1 &&
      (forall i :: 0 <= i < |history| ==> next[i] == history[i]) &&
      (forall i :: 0 <= i < |next| ==> ValidRunTrace(next[i]))
  {}
}
