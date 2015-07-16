TODO


Design & logic:
* Cursor state after false move must be consistent accross all cursor implementations and must be either reset
or previous state before the false move.
* IUpdateble is not implemented for mutable maps

* Buffer forward-only cursor series, e.g. scan: internal buffer must be subscribed to 
the source with a CT that is cancelled on dispose. Create buffer only when needed, do not create it 
on MoveFirst/Next (i.e. I[Async]Enumerator could and should be lazy, only navigation requires buffering)

Performance:
* Native math provider for batch operation


Comment prefix codes
* `NB`` means pay attantion to some non-obvious logic
* `NB!` means pay a lot of attention because this could be a source of potential ot fixed bugs
* `TODO` (`priority`) means:
    - no priority, just normal TODO, uncategorized: could be a source of bug, never spend time on (`low`, `very low`) while uncategorized issues exit (mark them low at least)
    - `high` - first priority,
    - `low` means could live with it,
    - `very low` is premature optimization or some puristic unimportant stuff
    - `performance` there are reasons to believe that performance could be improved at least twice; treat it as `low` 
* `TODO!` means same as `TODO (high)` + `NB!`