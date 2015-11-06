TODO
* (potential bug) In some places I mistakenly assumed that a comparer returns 1, 0, or -1. However, this is wrong and we should compare the returned value with 0.
* Features on par with Deedle/Ix, e.g. aggregate/resample, Panels must support what Frames do when it makes sense

Design & logic:
* Cursor state after false move must be consistent accross all cursor implementations and must be either reset
or previous state before the false move.
* IUpdateble is not implemented for SCM

* Buffer forward-only cursor series, e.g. scan: internal buffer must be subscribed to 
the source with a CT that is cancelled on dispose. Create buffer only when needed, do not create it 
on MoveFirst/Next (i.e. I[Async]Enumerator could and should be lazy, only navigation requires buffering)
* We could use conditional weak table + a buffer with max length of any open window over series.

Performance:
* Native math provider for batch operation (update: however, still cannot show huge perf gain. Probably need to implement immutable keys borrowing and Zip on batches)


Comment prefix codes
* `NB` means pay attantion to some non-obvious logic
* `NB!` means pay a lot of attention because this could be a source of potential ot fixed bugs
* `TODO` (`priority`) means:
    - no priority, just normal TODO, uncategorized: could be a source of bug, never spend time on (`low`, `very low`) while uncategorized issues exit (mark them low at least)
    - `high` - first priority,
    - `low` means could live with it,
    - `very low` is premature optimization or some puristic unimportant stuff
    - `perf`/`performance` there are reasons to believe that performance could be improved significantly; treat it as `low` unless with `!`
* `TODO!` means same as `TODO (high)` + `NB!`
* `TODO!!` or `TODO!!!` means that there was 5AM or someone was on a plane or any other urgency to keep the code as is.


Test
* For corectness and performance, we must generate random sets and use some well-know correct method, e.g. LINQ or Deedle. Each test that utilizes series functionality
 must be significantly faster than LINQ/Deedle, otherwise it makes no sense to use Spreads.
* Use Deedle as a benchmark for performance
