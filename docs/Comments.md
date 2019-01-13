TODOs
===========

[ ] Documentation
[ ] More correcness tests
[ ] Test and optimize complex calculation trees

Features

* [ ] Panels - series of panel member (dynamic DataFrame)
* [ ] DataFrames - still not sure if we should reinvent another one with some real-time support or just limit the use case to Panle range materialization.
* [ ] More operations (GroupBy etc)

Tests

* [ ] For corectness and performance, we must generate random sets and use some well-know correct method, e.g. LINQ or Deedle. Each test that utilizes series functionality
 must be significantly faster than LINQ/Deedle, otherwise it makes no sense to use Spreads.
* [ ] Benchmark suite that could keep history. Should use DS.


Codyng style
============

Mostly C# 7.3(8.0) + ReSharper + CodeMaid defaults (Ctrl+E+D + Ctrl+M+Space).

Internal fields are often named as private ones with `_lowerCamelCase`, which usually means that their usage 
outside the defining class/struct is for testing or optimization purposes, relies on implementation details and
is not safe to use in a general case even inside Spreads library itself.
