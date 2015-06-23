TODO

Design:
* IUpdateble is not implemented for mutable maps


Premature optimization:
* Implement SM and SCM cursors as sealed classes with fields instead
of F# object expressions and reference cells. ILSpy shows too much unneeded stuff.