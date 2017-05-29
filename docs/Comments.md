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


TODOs
===========

[ ] Write introduction without Rx, about series, declarative transofrmations, cursors, how to write new
cursors with examples
[ ] Add nice tables/charts with performance benchmarks, compared to Deedle, Streams, LINQ
[ ] Move above prefixes to contributing section, add CLA

Features

* [ ] Features on par with Deedle/Ix/Linq/Streams, e.g. aggregate/resample, Panels must support what Frames do when it makes sense


Tests

* [ ] For corectness and performance, we must generate random sets and use some well-know correct method, e.g. LINQ or Deedle. Each test that utilizes series functionality
 must be significantly faster than LINQ/Deedle, otherwise it makes no sense to use Spreads.
* [ ] Benchmark suite that could keep history.


Codyng style
============

Mostly C# 7.0 + ReSharper + CodeMaid defaults (Ctrl+E+D + Ctrl+M+Space).

Internal fields are often named as private ones with `_lowerCamelCase`, which usually means that their usage 
outside the defining class/struct is for testing or optimization purposes, relies on implementation details and
is not safe to use in a general case even inside Spreads library itself.
