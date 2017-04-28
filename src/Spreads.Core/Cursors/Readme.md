CursorSeries implementations
============================

This folder has implementations of CursorSeries from #108.

CursorSeries are object that implement ICursor<TKey,TValue> and ICursor<TKey,TValue> 
and provide series transformation and join functionality.

Some notes on implementation:

* CursorSeries are specialized on TCursor type. We do not hide types behind interfaces but rather
keep any deedply nested type chains. This greatly improves performance and as a side effect allows
to see the transformations chain right in the VS text editor as a pop-up during design time (doesn't even requires Debug).
(TODO Picture with the popup)

* All construtors of CursorSeries implementations are internal because we do unsafe cast of ICursor to TCursor
and must ensure that these constructors is called only when we guarantee the cast is correct.