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