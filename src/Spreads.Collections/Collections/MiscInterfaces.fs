//Copyright 2010-2013
//
//    Steffen Forkmann (http://navision-blog.de/)
//    Tomas Petricek (http://tomasp.net/)
//    Ryan Riley (http://panesofglass.org/)
//    Mauricio Scheffer (http://bugsquash.blogspot.com/)
//    Jack Fox (http://jackfoxy.com/)
//
//Licensed under the Apache License, Version 2.0 (the "License");

// Spreads combined several files from https://github.com/fsprojects/FSharpx.Collections/


// TODO (clean) this was for a stupid panel algo, delete it
namespace Spreads.Collections.Obsolete

type IPriorityQueue<'T when 'T : comparison> =
    inherit System.Collections.IEnumerable
    inherit System.Collections.Generic.IEnumerable<'T>

    ///returns true if the queue has no elements
    abstract member IsEmpty : bool with get

    ///returns a new queue with the element added to the end
    abstract member Insert : 'T -> IPriorityQueue<'T>

    ///returns option first element
    abstract member TryPeek : unit -> 'T option

    ///returns the first element
    abstract member Peek : unit -> 'T

    //returns the option first element and tail
    abstract member TryPop : unit -> ('T * IPriorityQueue<'T>) option

    ///returns the first element and tail
    abstract member Pop : unit -> 'T * IPriorityQueue<'T> 