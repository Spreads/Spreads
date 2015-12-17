Comment prefix codes:

* `NB` means pay attantion to some non-obvious logic
* `NB!` means pay a lot of attention because this could be a source of potential ot fixed bugs
* `TODO` (`priority`) means:
    - no priority, just normal TODO, uncategorized: could be a source of bug, never spend time on (`low`, `very low`) while uncategorized issues exit (mark them low at least)
    - `bug` - zero priority,
    - `high` - first priority,
    - `low` means could live with it until better times,
    - `very low` is premature optimization or some puristic unimportant stuff
    - `perf`/`performance` there are reasons to believe that performance could be improved significantly; treat it as `low` unless with `!`
    - `ux` - user experience with API: signatures, comments, etc.
* `TODO!` means same as `TODO (high)` + `NB!`
* `TODO!!` or `TODO!!!` means that there was 5AM or someone was on a plane or was drunk or there was any other urgency to keep the code as is, with high probability of mistakes.

Docs

[ ] Write introduction without Rx, about series, declarative transofrmations, cursors, how to write new
cursors with examples
[ ] Add nice tables/charts with performance benchmarks, compared to Deedle, Streams, LINQ
[ ] Move above prefixes to continuting section, add CLA

Features

* [ ] Features on par with Deedle/Ix/Linq/Streams, e.g. aggregate/resample, Panels must support what Frames do when it makes sense

Design & logic:

* [ ] Cursor state after false move must be consistent accross all cursor implementations 
and must be either reset or previous state before the false move. IT MUST BE UNCHANGED
PREVIOUS STATE FOR MoveNext() -> false due to our MN/MNAsync contracts.
* [x] IUpdateble is not implemented for SCM

* [ ] (partially implemented in Scan bufefr and ToSortedMap WeakReference) Buffer forward-only cursor series, e.g. scan: internal buffer must be subscribed to 
the source with a CT that is cancelled on dispose. Create buffer only when needed, do not create it 
on MoveFirst/Next (i.e. I[Async]Enumerator could and should be lazy, only navigation requires buffering)
* [ ] We could use conditional weak table + a buffer with max length of any open window over series. Or something
much simpler and explicit.

Performance:

* [ ] SM and SCM cursors should be implemented as structs and SCM should work directly with internals of inner
of SM cursors. Currently, SCM iteration spead even wihtout load is not so good - 15 MOps, vs 30 MOps of triple 
chained arithmetic ops over SM.
* [ ] Native math provider for batch operation (update: however, still cannot show huge perf gain. 
Probably need to implement immutable keys borrowing and Zip on batches)
* [ ] From above: key borrowings or array pools/regular keys equality check. 
* [ ] Heuristic batching in Zip - when cursors are at the same key for N moves, we could batch them and then
zip batches. However, for simple things gains are not obvious and need to benchmark. Savings could come from
 avoiding method calls and/or from SIMD optimizations.



Tests

* [ ] For corectness and performance, we must generate random sets and use some well-know correct method, e.g. LINQ or Deedle. Each test that utilizes series functionality
 must be significantly faster than LINQ/Deedle, otherwise it makes no sense to use Spreads.
* [ ] Use Deedle as a base level for performance, then we could get nice numbers :)
* [ ] Benchmark suite that could keep history. There is an interesting project: https://github.com/PerfDotNet/BenchmarkDotNet,
however the simplest `perf` functions works well enough to get the big picture.
