namespace Spreads

open Spreads
open System
open System.Numerics
open System.Reflection
open FSharp.Core
open Microsoft.FSharp.Core
open Microsoft.FSharp.Core.LanguagePrimitives.IntrinsicOperators
open Microsoft.FSharp.Collections
open Microsoft.FSharp.Core.Operators
open Spreads.SIMDArrayUtils

[<RequireQualifiedAccess>]
module SIMD =

  /// <summary>
  /// First does skipWhile one vector at a time using vf. If vf returns false
  /// Then narrows doen the exact value with sf.
  /// </summary>
  /// <param name="vf"></param>
  /// <param name="sf"></param>
  /// <param name="array"></param>
  let inline skipWhile (vf : Vector< ^T> -> bool) (sf : ^T -> bool) (array : ^T[]) : ^T[] =
      checkNonNull array
      if array.Length <> 0 then
          let mutable i = 0
          let count = Vector< ^T>.Count    
          let len = array.Length
          while i <= len-count && vf (Vector< ^T>(array,i)) do        
              i <- i + count
        
          if i <= len then     
              i <- System.Math.Max(i - count,0)
              while i < array.Length && sf array.[i] do
                  i <- i + 1                        
              Array.sub array i (len - i)            
          else
              Array.empty
      else
          Array.empty

  /// <summary>
  /// First does takeWhile one vector at a time using vf. If vf returns false
  /// Then narrows doen the exact value with sf.
  /// </summary>
  /// <param name="vf"></param>
  /// <param name="sf"></param>
  /// <param name="array"></param>
  let inline takeWhile (vf : Vector< ^T> -> bool) (sf : ^T -> bool) (array : ^T[]) : ^T[] =
      checkNonNull array

      if array.Length <> 0 then
          let mutable i = 0
          let count = Vector< ^T>.Count    
          let len = array.Length
          while i <= len-count && vf (Vector< ^T>(array,i)) do        
              i <- i + count

          if i <= len then            
              i <- System.Math.Max(i - count,0)
              while i < array.Length && sf array.[i] do
                  i <- i + 1            
              Array.sub array 0 i
          else
              Array.empty
      else
          Array.empty

  /// <summary>
  /// mapFold
  /// </summary>
  /// <param name="vf"></param>
  /// <param name="sf"></param>
  /// <param name="combiner"></param>
  /// <param name="acc"></param>
  /// <param name="array"></param>
  let inline mapFold
      (vf: ^State Vector -> ^T Vector -> ^U Vector * ^State Vector)
      (sf : ^State -> ^T -> ^U * ^State)
      (combiner : ^State -> ^State -> ^State)
      (acc : ^State)
      (array: ^T[]) : ^U[] * ^State =
    
      checkNonNull array
        
      let count = Vector< ^T>.Count
    
      let mutable state = Vector< ^State> acc
      let mutable i = 0    
      let res = Array.zeroCreate array.Length 
      while i <= array.Length-count do
          let (x,newstate) = vf state (Vector< ^T>(array,i))
          x.CopyTo(res,i)
          state <- newstate
          i <- i + count

      let mutable result = acc
      while i < array.Length do
          let (x,newstate) = sf result array.[i]
          result <- newstate
          res.[i] <- x

          i <- i + 1
                   
      i <- 0    
      while i < Vector< ^State>.Count do
          result <- combiner result state.[i]
          i <- i + 1
      res,result

  /// <summary>
  /// mapFoldBack
  /// </summary>
  /// <param name="vf"></param>
  /// <param name="sf"></param>
  /// <param name="combiner"></param>
  /// <param name="acc"></param>
  /// <param name="array"></param>
  let inline mapFoldBack
      (vf:  ^T Vector -> ^State Vector -> ^U Vector * ^State Vector)
      (sf : ^T -> ^State -> ^U * ^State)
      (combiner : ^State -> ^State -> ^State)
      (array: ^T[])
      (acc : ^State) : ^U[] * ^State =


      checkNonNull array
        
      let count = Vector< ^T>.Count
    
      let mutable state = Vector< ^State> acc
      let mutable i = array.Length-count
      let res = Array.zeroCreate array.Length 
      while i >= 0 do
          let (x,newstate) = vf (Vector< ^T>(array,i)) state
          x.CopyTo(res,i)
          state <- newstate
          i <- i - count

      let mutable result = acc
      i <- i + count - 1
      while i >= 0 do
          let (x,newstate) = sf array.[i] result
          result <- newstate
          res.[i] <- x
          i <- i - 1
                   
      i <- Vector< ^State>.Count - 1    
      while i >= 0 do
          result <- combiner result state.[i]
          i <- i - 1
      res,result


  /// <summary>
  /// Similar to the standard Fold functionality but you must also provide a combiner
  /// function to combine each element of the Vector at the end. Not that acc
  /// can be double applied, this will not behave the same as fold. Typically
  /// 0 will be used for summing operations and 1 for multiplication.
  /// </summary>
  /// <param name="f">The folding function</param>
  /// <param name="combiner">Function to combine the Vector elements at the end</param>
  /// <param name="acc">Initial value to accumulate from</param>
  /// <param name="array">Source array</param>
  let inline fold
      (vf: ^State Vector -> ^T Vector -> ^State Vector)
      (sf : ^State -> ^T -> ^State)
      (combiner : ^State -> ^State -> ^State)
      (acc : ^State)
      (array: ^T[]) : ^State =

      checkNonNull array
        
      let count = Vector< ^T>.Count
    
      let mutable state = Vector< ^State> acc
      let mutable i = 0    
      while i <= array.Length-count do
          state <- vf state (Vector< ^T>(array,i))
          i <- i + count

      let mutable result = acc
      while i < array.Length do
          result <- sf result array.[i]
          i <- i + 1
                   
      i <- 0    
      while i < Vector< ^State>.Count do
          result <- combiner result state.[i]
          i <- i + 1
      result

  /// <summary>
  /// Similar to the standard FoldBack functionality but you must also provide a combiner
  /// function to combine each element of the Vector at the end. Not that acc
  /// can be double applied, this will not behave the same as foldback. Typically
  /// 0 will be used for summing operations and 1 for multiplication.
  /// </summary>
  /// <param name="f">The folding function</param>
  /// <param name="combiner">Function to combine the Vector elements at the end</param>
  /// <param name="acc">Initial value to accumulate from</param>
  /// <param name="array">Source array</param>
  let inline foldBack
      (vf: ^State Vector -> ^T Vector -> ^State Vector)
      (sf : ^State -> ^T -> ^State)
      (combiner : ^State -> ^State -> ^State)    
      (array: ^T[]) 
      (acc : ^State) : ^State  =

      checkNonNull array        
      let count = Vector< ^T>.Count    

      let mutable state = Vector< ^State> acc
      let mutable i = array.Length-count
      while i >= 0 do
          state <- vf state (Vector< ^T>(array,i))
          i <- i - count

      let mutable result = acc
      i <- i + count - 1
      while i >= 0 do
          result <- sf result array.[i]
          i <- i - 1
                   
      i <- Vector< ^State>.Count - 1
      while i >= 0  do
          result <- combiner result state.[i]
          i <- i - 1
      result

  /// <summary>
  /// Similar to the standard Fold2 functionality but you must also provide a combiner
  /// function to combine each element of the Vector at the end. Not that acc
  /// can be double applied, this will not behave the same as fold2back. Typically
  /// 0 will be used for summing operations and 1 for multiplication.
  /// </summary>
  /// <param name="f">The folding function</param>
  /// <param name="combiner">Function to combine the Vector elements at the end</param>
  /// <param name="acc">Initial value to accumulate from</param>
  /// <param name="array">Source array</param>
  let inline fold2
      (vf : ^State Vector -> ^T Vector -> ^U Vector -> ^State Vector)   
      (sf : ^State -> ^T -> ^U -> ^State)
      (combiner : ^State -> ^State -> ^State)
      (acc : ^State)
      (array1: ^T[])
      (array2: ^U[]) : ^State =

      checkNonNull array1
      checkNonNull array2

      let count = Vector< ^T>.Count    
      if count <> Vector< ^U>.Count then invalidArg "array" "Inputs and output must all have same Vector width."
    
      let len = array1.Length        
      if len <> array2.Length then invalidArg "array2" "Arrays must have same length"
            
    
      let mutable state = Vector< ^State> acc
      let mutable i = 0    
      while i <= len-count do
          state <- vf state (Vector< ^T>(array1,i)) (Vector< ^U>(array2,i))
          i <- i + count


      let mutable result = acc
      while i < array1.Length do
          result <- sf result array1.[i] array2.[i]
          i <- i + 1 
        
      i <- 0    
      while i < Vector< ^State>.Count do
          result <- combiner result state.[i]
          i <- i + 1
      result

  /// <summary>
  /// Similar to the standard foldBack2 functionality but you must also provide a combiner
  /// function to combine each element of the Vector at the end. Not that acc
  /// can be double applied, this will not behave the same as foldBack2. Typically
  /// 0 will be used for summing operations and 1 for multiplication.
  /// </summary>
  /// <param name="f">The folding function</param>
  /// <param name="combiner">Function to combine the Vector elements at the end</param>
  /// <param name="acc">Initial value to accumulate from</param>
  /// <param name="array">Source array</param>
  let inline foldBack2
      (vf : ^State Vector -> ^T Vector -> ^U Vector -> ^State Vector)   
      (sf : ^State -> ^T -> ^U -> ^State)
      (combiner : ^State -> ^State -> ^State)
      (array1: ^T[])
      (array2: ^U[])
      (acc : ^State) : ^State =

      checkNonNull array1
      checkNonNull array2

      let count = Vector< ^T>.Count    
      if count <> Vector< ^U>.Count then invalidArg "array" "Inputs and output must all have same Vector width."
    
      let len = array1.Length        
      if len <> array2.Length then invalidArg "array2" "Arrays must have same length"
                
      let mutable state = Vector< ^State> acc
      let mutable i = array1.Length-count 
      while i >= 0 do
          state <- vf state (Vector< ^T>(array1,i)) (Vector< ^U>(array2,i))
          i <- i - count

      let mutable result = acc
      i <- i + count - 1
      while i >= 0 do
          result <- sf result array1.[i] array2.[i]
          i <- i - 1 
        
      i <- Vector< ^State>.Count - 1    
      while i >= 0 do
          result <- combiner result state.[i]
          i <- i - 1
      result


  /// <summary>
  /// A convenience function to call Fold with an acc of 0
  /// </summary>
  /// <param name="f">The folding function</param>
  /// <param name="combiner">Function to combine the Vector elements at the end</param>
  /// <param name="array">Source array</param>
  let inline reduce
      (vf: ^State Vector -> ^T Vector -> ^State Vector)
      (sf: ^State -> ^T -> ^State )
      (combiner : ^State -> ^State -> ^State)
      (array: ^T[]) : ^State =
      fold vf sf combiner Unchecked.defaultof< ^State> array    
    

  /// <summary>
  /// A convenience function to call FoldBack with an acc of 0
  /// </summary>
  /// <param name="f">The folding function</param>
  /// <param name="combiner">Function to combine the Vector elements at the end</param>
  /// <param name="array">Source array</param>
  let inline reduceBack
      (vf: ^State Vector -> ^T Vector -> ^State Vector)
      (sf: ^State -> ^T -> ^State )
      (combiner : ^State -> ^State -> ^State)
      (array: ^T[]) : ^State =
      foldBack vf sf combiner  array Unchecked.defaultof< ^State>

    
        

  /// <summary>
  /// Creates an array filled with the value x. 
  /// </summary>
  /// <param name="count">How large to make the array</param>
  /// <param name="x">What to fille the array with</param>
  let inline create (count :int) (x:^T) =
    
      if count < 0 then invalidArg "count" "The input must be non-negative."

      let array = Array.zeroCreate count
      let v = Vector< ^T> x
      let vCount = Vector< ^T>.Count    

      let mutable i = 0
      while i <= array.Length-vCount do
          v.CopyTo(array,i)
          i <- i + vCount

      i <- array.Length-array.Length%vCount
      while i < array.Length && i >= 0 do
          array.[i] <- x
          i <- i + 1

      array

  /// <summary>
  /// Creates an array filled with the vector X, repeating it count times. 
  /// </summary>
  /// <param name="count">How large to make the array</param>
  /// <param name="x">What to fille the array with</param>
  let inline createVector (count :int) (v:Vector< ^T>) =
    
      if count < 0 then invalidArg "count" "The input must be non-negative."

      let vCount = Vector< ^T>.Count
      let array = Array.zeroCreate (count * vCount)
                    
      let mutable i = 0
      while i <= array.Length-vCount do
          v.CopyTo(array,i)
          i <- i + vCount    
      array

  /// <summary>
  /// Creates an array filled with the value x. 
  /// </summary>
  /// <param name="count">How large to make the array</param>
  /// <param name="x">What to fille the array with</param>
  let inline replicate (count :int) (x:^T) = 
      create count x

  /// <summary>
  /// Fills an array filled with the value x. 
  /// </summary>
  /// <param name="count">How large to make the array</param>
  /// <param name="x">What to fille the array with</param>
  let inline fill (array: ^T[]) (index: int) (count :int) (x:^T) =
    
      if count < 0 || count > array.Length then invalidArg "count" "The count was invalid."
      if index < 0 || index > array.Length then invalidArg "index" "The index was invalid."
           
      printf "index: %A count: %A  x:%A\n" index count x
      let v = Vector< ^T> x
      let vCount = Vector< ^T>.Count
    
      let mutable i = index
      while i <= array.Length-vCount do
          v.CopyTo(array,i)
          i <- i + vCount

      i <- array.Length-array.Length%vCount
      while i < index+count  do
          array.[i] <- x
          i <- i + 1
    
  /// <summary>
  /// Sets a range of an array to the default value.
  /// </summary>
  /// <param name="array">The array to clear</param>
  /// <param name="index">The starting index to clear</param>
  /// <param name="length">The number of elements to clear</param>
  let inline clear (array : ^T[]) (index : int) (length : int) : unit =
    
      let v = Vector< ^T>.Zero
      let vCount = Vector< ^T>.Count
      let lenLessCount = length-vCount

      let mutable i = index
      while i <= lenLessCount do
          v.CopyTo(array,i)
          i <- i + vCount

      i <- array.Length-array.Length%vCount
      while i < length do
          array.[i] <- Unchecked.defaultof< ^T>
          i <- i + 1



  /// <summary>
  /// Similar to the built in init function but f will get called with every
  /// nth index, where n is the width of the vector, and you return a Vector.
  /// </summary>
  /// <param name="count">How large to make the array</param>
  /// <param name="f">A function that accepts every Nth index and returns a Vector to be copied into the array</param>
  let inline init (count :int) (vf : int -> Vector< ^T>) (sf : int -> ^T) =
    
      if count < 0 then invalidArg "count" "The input must be non-negative."
    
      let array = Array.zeroCreate count : ^T[]    
      let vCount = Vector< ^T>.Count
        
      let mutable i = 0
      while i <= count-vCount do
          (vf i).CopyTo(array,i)
          i <- i + vCount
    
      i <- array.Length-array.Length%vCount
      while i < array.Length do
          array.[i] <- sf i       
          i <- i + 1

      array


  /// <summary>
  /// Sums the elements of the array
  /// </summary>
  /// <param name="array"></param>
  let inline sum (array:^T[]) : ^T =

      checkNonNull array

      let mutable state = Vector< ^T>.Zero    
      let count = Vector< ^T>.Count
        
      let mutable i = 0
      while i <= array.Length-count do
          state <-  state + Vector< ^T>(array,i)
          i <- i + count

      let mutable result = Unchecked.defaultof< ^T>
      i <- array.Length-array.Length%count
      while i < array.Length do
          result <- result + array.[i]
          i <- i + 1

      i <- 0
      while i < count do
          result <- result + state.[i]
          i <- i + 1
      result

  /// <summary>
  /// Sums the elements of the array by applying the function to each Vector of the array.
  /// </summary>
  /// <param name="array"></param>
  let inline sumBy 
      (vf: Vector< ^T> -> Vector< ^U>) 
      (sf : ^T -> ^U) 
      (array:^T[]) : ^U =

      checkNonNull array
    
      let mutable state = Vector< ^U>.Zero    
      let count = Vector< ^T>.Count
    
      let mutable i = 0
      while i <= array.Length-count do
          state <-  state + vf (Vector< ^T>(array,i))
          i <- i + count
    
      let mutable result = Unchecked.defaultof< ^U>    
      i <- array.Length-array.Length%count
      while i < array.Length do
          result <- result + sf array.[i]
          i <- i + 1

      i <- 0
      while i < count do
          result <- result + state.[i]
          i <- i + 1
      result

  /// <summary>
  /// Computes the average of the elements in the array
  /// </summary>
  /// <param name="array"></param>
  let inline average (array:^T[]) : ^T =
      let sum = sum array
      LanguagePrimitives.DivideByInt< ^T> sum array.Length
    

  /// <summary>
  /// Computes the average of the elements in the array by applying the function to
  /// each Vector of the array
  /// </summary>
  /// <param name="array"></param>
  let inline averageBy 
      (vf: Vector< ^T> -> Vector< ^U>) (sf: ^T -> ^U) (array:^T[]) : ^U =
      let sum = sumBy vf sf array
      LanguagePrimitives.DivideByInt< ^U> sum array.Length


  /// <summary>
  /// Identical to the standard map function, but you must provide
  /// A Vector mapping function.
  /// </summary>
  /// <param name="vf">A function that takes a Vector and returns a Vector. The returned vector
  /// does not have to be the same type but must be the same width</param>
  /// <param name="sf">A function to handle the leftover scalar elements if array is not divisible by Vector.count</param>
  /// <param name="array">The source array</param>
  let inline map
      (vf : ^T Vector -> ^U Vector) (sf : ^T -> ^U) (array : ^T[]) : ^U[] =

      checkNonNull array
      let count = Vector< ^T>.Count
      if count <> Vector< ^U>.Count then invalidArg "array" "Output type must have the same width as input type."    
    
      let result = Array.zeroCreate array.Length
    
      let mutable i = 0
      while i <= array.Length-count do        
          (vf (Vector< ^T>(array,i ))).CopyTo(result,i)   
          i <- i + count
    
      i <- array.Length-array.Length%count
      while i < result.Length do
          result.[i] <- sf array.[i]
          i <- i + 1

      result

  let inline mapSegment
    (vf : 'T Vector -> 'U Vector) (sf : ^T -> ^U) (segment : ArraySegment<'T>) : ArraySegment<'U> =
    //Console.WriteLine("SIMD mapSegment")
    let count = Vector<'T>.Count
    if count <> Vector<'U>.Count then invalidArg "array" "Output type must have the same width as input type."    
    
    let result = Impl.ArrayPool<'U>.Rent(segment.Count) //Array.zeroCreate segment.Array.Length //
    
    let mutable i = segment.Offset
    let length = i + segment.Count
    while i <= length-count do        
        (vf (Vector<'T>(segment.Array,i ))).CopyTo(result,i)   
        i <- i + count
    
    i <- length - length % count
    while i < result.Length do
        result.[i] <- sf segment.Array.[i]
        i <- i + 1

    ArraySegment(result, 0, segment.Count)

  /// <summary>
  /// Identical to the standard map2 function, but you must provide
  /// A Vector mapping function.
  /// </summary>
  /// <param name="f">A function that takes two Vectors and returns a Vector. Both vectors and the
  /// returned vector do not have to be the same type but must be the same width</param>
  /// <param name="array">The source array</param>
  let inline map2
      (vf : ^T Vector -> ^U Vector -> ^V Vector) 
      (sf : ^T -> ^U -> ^V)
      (array1 : ^T[]) 
      (array2 :^U[]) : ^V[] =

      checkNonNull array1
      checkNonNull array2

      let count = Vector< ^T>.Count    
      if count <> Vector< ^U>.Count || count <> Vector< ^V>.Count then invalidArg "array" "Inputs and output must all have same Vector width."
    
      let len = array1.Length        
      if len <> array2.Length then invalidArg "array2" "Arrays must have same length"

      let result = Array.zeroCreate len
    
      let mutable i = 0    
      while i <= len-count do
          (vf (Vector< ^T>(array1,i )) (Vector< ^U>(array2,i))).CopyTo(result,i)   
          i <- i + count
    
      i <- len-len%count
      while i < result.Length do
          result.[i] <- sf array1.[i] array2.[i]
          i <- i + 1

      result
    

  /// <summary>
  /// Identical to the standard map2 function, but you must provide
  /// A Vector mapping function.
  /// </summary>
  /// <param name="f">A function that takes three Vectors and returns a Vector. All vectors and the
  /// returned vector do not have to be the same type but must be the same width</param>
  /// <param name="array">The source array</param>


  let inline map3
      (vf : ^T Vector -> ^U Vector -> ^V Vector -> ^W Vector) 
      (sf : ^T -> ^U -> ^V -> ^W)
      (array1 : ^T[]) (array2 :^U[]) (array3 :^V[]): ^W[] =

      checkNonNull array1
      checkNonNull array2
      checkNonNull array3

      let count = Vector< ^T>.Count    
      if count <> Vector< ^U>.Count || count <> Vector< ^V>.Count || count <> Vector< ^W>.Count then invalidArg "array" "Inputs and output must all have same Vector wdith"
    
      let len = array1.Length        
      if len <> array2.Length || len <> array3.Length then invalidArg "array2" "Arrays must have same length"
    
      let result = Array.zeroCreate len
    
      let mutable i = 0    
      while i <= len - count do
          (vf (Vector< ^T>(array1,i )) (Vector< ^U>(array2,i)) (Vector< ^V>(array3,i))).CopyTo(result,i)        
          i <- i + count
        
      i <- len-len%count
      while i < result.Length do
          result.[i] <- sf array1.[i] array2.[i] array3.[i]
          i <- i + 1
    

      result
  /// <summary>
  /// Identical to the standard mapi2 function, but you must provide
  /// A Vector mapping function.
  /// </summary>
  /// <param name="f">A function that takes two Vectors and an index 
  /// and returns a Vector. All vectors must be the same width</param>
  /// <param name="array">The source array</param>

  let inline mapi2
      (vf : int -> ^T Vector -> ^U Vector -> ^V Vector) 
      (sf : int -> ^T -> ^U -> ^V)
      (array1 : ^T[]) (array2 :^U[]) : ^V[] =

      checkNonNull array1
      checkNonNull array2

      let count = Vector< ^T>.Count    
      if count <> Vector< ^U>.Count || count <> Vector< ^V>.Count then invalidArg "array" "Inputs and output must all have same Vector wdith"
    
      let len = array1.Length        
      if len <> array2.Length then invalidArg "array2" "Arrays must have same length"
    
      let result = Array.zeroCreate len
    
      let mutable i = 0    
      while i <= len-count do
          (vf i (Vector< ^T>(array1,i )) (Vector< ^U>(array2,i))).CopyTo(result,i)        
          i <- i + count
        
      i <- len-len%count
      while i < result.Length do
          result.[i] <- sf i array1.[i] array2.[i]
          i <- i + 1

      result

  /// <summary>
  /// Identical to the standard mapi function, but you must provide
  /// A Vector mapping function.
  /// </summary>
  /// <param name="f">A function that takes the current index and it's Vector and returns a Vector. The returned vector
  /// does not have to be the same type but must be the same width</param>
  /// <param name="array">The source array</param>
  let inline mapi
      (vf : int -> ^T Vector -> ^U Vector) 
      (sf: int -> ^T -> ^U)
      (array : ^T[]) : ^U[] =

      checkNonNull array
      let count = Vector< ^T>.Count
      if count <> Vector< ^U>.Count then invalidArg "array" "Output type must have the same width as input type."
    
      let len = array.Length    
      let result = Array.zeroCreate len    
    
      let mutable i = 0    
      while i <= len-count do
          (vf i (Vector< ^T>(array,i ))).CopyTo(result,i)                
          i <- i + count
        
      i <- len-len%count
      while i < result.Length do
          result.[i] <- sf i array.[i]
          i <- i + 1
    
      result

  /// <summary>
  /// Iterates over the array applying f to each Vector sized chunk
  /// </summary>
  /// <param name="f">Accepts a Vector</param>
  /// <param name="array"></param>
  let inline iter
      (vf : Vector< ^T> -> unit) 
      (sf : ^T -> unit) 
      (array : ^T[]) : unit  =

      checkNonNull array
        
      let len = array.Length        
      let count = Vector< ^T>.Count
    
      let mutable i = 0    
      while i <= len-count do
          vf (Vector< ^T>(array,i ))
          i <- i + count
    
      i <- len-len%count
      while i < array.Length do
          sf array.[i]
          i <- i + 1
    

  /// <summary>
  /// Iterates over the two arrays applying f to each Vector pair
  /// </summary>
  /// <param name="f">Accepts two Vectors</param>
  /// <param name="array"></param>
  let inline iter2 
      (vf : Vector< ^T> -> Vector< ^U> -> unit)
      (sf : ^T -> ^U -> unit)
      (array1: ^T[]) (array2: ^U[]) : unit =

      checkNonNull array1
      checkNonNull array2

      let count = Vector< ^T>.Count    
      if count <> Vector< ^U>.Count then invalidArg "array" "Inputs and output must all have same Vector width."
    
      let len = array1.Length        
      if len <> array2.Length then invalidArg "array2" "Arrays must have same length"
    
      let mutable i = 0
      while i <= len-count do 
          vf (Vector< ^T>(array1,i)) (Vector< ^U>(array2,i))
          i <- i + count

      i <- len-len%count
      while i < array1.Length do
          sf array1.[i] array2.[i]
          i <- i + 1
    

  /// <summary>
  /// Iterates over the array applying f to each Vector sized chunk
  /// along with the current index.
  /// </summary>
  /// <param name="f">Accepts the current index and associated Vector</param>
  /// <param name="array"></param>
  let inline iteri
      (vf : int -> Vector< ^T> -> unit)
      (sf : int -> ^T -> unit)
      (array : ^T[]) : unit  =

      checkNonNull array
      let len = array.Length
             
      let count = Vector< ^T>.Count    

      let mutable i = 0    
      while i <= len-count do
          vf i (Vector< ^T>(array,i ))
          i <- i + count

      i <- len-len%count
      while i < array.Length do
          sf i array.[i]
          i <- i + 1
        
    
  /// <summary>
  /// Iterates over the two arrays applying f to each Vector pair
  /// and their current index.
  /// </summary>
  /// <param name="f">Accepts two Vectors</param>
  /// <param name="array"></param>
  let inline iteri2 
      (vf : int -> Vector< ^T> -> Vector< ^U> -> unit)
      (sf : int -> ^T -> ^U -> unit)
      (array1: ^T[]) (array2: ^U[]) : unit =

      checkNonNull array1
      checkNonNull array2
    
      let count = Vector< ^T>.Count    
      if count <> Vector< ^U>.Count then invalidArg "array" "Inputs and output must all have same Vector width."
    
      let len = array1.Length        
      if len <> array2.Length then invalidArg "array2" "Arrays must have same length"
    
      let mutable i = 0
      while i <= len-count do 
          vf i (Vector< ^T>(array1,i)) (Vector< ^U>(array2,i))
          i <- i + count

      i <- len-len%count
      while i < array1.Length do
          sf i array1.[i] array2.[i]
          i <- i + 1
    
  /// <summary>
  /// Identical to the SIMDMap except the operation is done in place, and thus
  /// the resulting Vector type must be the same as the intial type. This will
  /// perform better when it can be used.
  /// </summary>
  /// <param name="f">Mapping function that takes a Vector and returns a Vector of the same type</param>
  /// <param name="array"></param>

  let inline mapInPlace
      ( vf : ^T Vector -> ^T Vector) 
      ( sf : ^T -> ^T )
      (array: ^T[]) : unit =

      checkNonNull array
    
      let len = array.Length
      let count = Vector< ^T>.Count
    
      let mutable i = 0
      while i <= len-count do
          (vf (Vector< ^T>(array,i ))).CopyTo(array,i)   
          i <- i + count
            
      i <- len-len%count   
      while i < array.Length do
          array.[i] <- sf array.[i]
          i <- i + 1
  
  /// <summary>
  /// Like the standard pick, but a vector at a time
  /// </summary>
  /// <param name="vf">Takes a Vector 'T and returns an option</param>
  /// <param name="sf">Takes a 'T and returns an option</param>
  /// <param name="array"></param>
  let inline pick
      (vf : ^T Vector -> ^U Option) (sf: ^T -> ^U Option) (array: ^T[]) : ^U =    
      checkNonNull array    

      let count = Vector< ^T>.Count
      let len = array.Length
      let mutable found = false    
    
      let mutable result = Unchecked.defaultof< ^U>
      let mutable i = 0
      while i <= len-count && not found do
          match vf (Vector< ^T>(array,i)) with
          | Some x -> result <- x; found <- true
          | None -> ()
          i <- i + count
    
      if found then        
          result        
      else    
          i <- len-len%count
          while i < array.Length && not found do
              match sf array.[i] with
              | Some x -> result <- x; found <- true
              | None -> ()
              i <- i + 1
          if found then 
              result
          else
              raise (System.Collections.Generic.KeyNotFoundException())

  /// <summary>
  /// Like the standard tryPick, but a vector at a time   
  /// </summary>
  /// <param name="vf">Takes a Vector 'T and returns an option</param>
  /// <param name="sf">Takes a 'T and returns an option</param>
  /// <param name="array"></param>
  let inline tryPick
      (vf : ^T Vector -> ^U Option) (sf: ^T -> ^U Option) (array: ^T[]) : ^U Option =

      checkNonNull array    

      let count = Vector< ^T>.Count
      let len = array.Length
      let mutable result = None
      let mutable i = 0
      while i <= len-count && result.IsNone do
          result <- vf (Vector< ^T>(array,i)) 
          i <- i + count
    
      if result.IsSome then        
          result        
      else    
          i <- len-len%count
          while i < array.Length && result.IsNone do
              result <- sf array.[i]
              i <- i + 1
          result
            
  /// <summary>
  /// Takes a function that accepts a vector and returns true or false, and
  /// a function that takes a single element and returns true or false.
  /// Returns the index of the first element that satisfies both predicates.
  /// returns KeyNotFoundException if not found
  /// </summary>
  /// <param name="vf">Takes a Vector 'T and returns true or false</param>
  /// <param name="sf">Takes a 'T and returns true or false</param>
  /// <param name="array"></param>
  let inline findIndex
      (vf : ^T Vector -> bool) (sf: ^T -> bool) (array: ^T[]) : int =

      checkNonNull array    

      let count = Vector< ^T>.Count
      let len = array.Length
      let mutable i = 0
      while i <= len-count && not (vf (Vector< ^T>(array,i))) do
          i <- i + count
    
      if i <= len-count then
          let v = Vector< ^T>(array,i)
          let mutable j = 0
          while j < count && not (sf v.[j]) do
              j <- j + 1    
              i <- i + 1    
          i                                          
      else    
          i <- len-len%count
          while i < array.Length && not (sf array.[i]) do
              i <- i + 1
          if i < len then
              i
          else
              raise (System.Collections.Generic.KeyNotFoundException())

  /// <summary>
  /// Takes a function that accepts a vector and returns true or false, and
  /// a function that takes a single element and returns true or false.
  /// Returns the value of the first element that satisfies both predicates.
  /// returns KeyNotFoundException if not found
  /// </summary>
  /// <param name="vf">Takes a Vector 'T and returns true or false</param>
  /// <param name="sf">Takes a 'T and returns true or false</param>
  /// <param name="array"></param>
  let inline find
      (vf : ^T Vector -> bool) (sf: ^T -> bool) (array: ^T[]) : ^T =

      array.[findIndex vf sf array]

  /// <summary>
  /// Takes a function that accepts a vector and returns true or false, and
  /// a function that takes a single element and returns true or false.
  /// Returns the index of the last element that satisfies both predicates.
  /// returns KeyNotFoundException if not found
  /// </summary>
  /// <param name="vf">Takes a Vector 'T and returns true or false</param>
  /// <param name="sf">Takes a 'T and returns true or false</param>
  /// <param name="array"></param>
  let inline findIndexBack
      (vf : ^T Vector -> bool) (sf: ^T -> bool) (array: ^T[]) : int =

      checkNonNull array    

      let count = Vector< ^T>.Count    
      let mutable i = array.Length-count
      while i >= 0 && not (vf (Vector< ^T>(array,i))) do
          i <- i - count
    
      if i >= 0 then
          let v = Vector< ^T>(array,i)
          i <- i + count - 1
          let mutable j = count-1
          while j >= 0 && not (sf v.[j]) do
              j <- j - 1                                    
              i <- i - 1
          i
      else    
          i <- i + count - 1
          while i >= 0 && not (sf array.[i]) do          
              i <- i - 1
          if i >= 0 then 
              i
          else
              raise (System.Collections.Generic.KeyNotFoundException())


  /// <summary>
  /// Takes a function that accepts a vector and returns true or false, and
  /// a function that takes a single element and returns true or false.
  /// Returns the value of the last element that satisfies both predicates.
  /// returns KeyNotFoundException if not found
  /// </summary>
  /// <param name="vf">Takes a Vector 'T and returns true or false</param>
  /// <param name="sf">Takes a 'T and returns true or false</param>
  /// <param name="array"></param>
  let inline findBack
      (vf : ^T Vector -> bool) (sf: ^T -> bool) (array: ^T[]) : ^T =

      array.[findIndexBack vf sf array]


  /// <summary>
  /// Takes a function that accepts a vector and returns true or false, and
  /// a function that takes a single element and returns true or false.
  /// Returns the Option index of the first element that satisfies both predicates
  /// or None if not found
  /// </summary>
  /// <param name="vf">Takes a Vector 'T and returns true or false</param>
  /// <param name="sf">Takes a 'T and returns true or false</param>
  /// <param name="array"></param>
  let inline tryFindIndex
       (vf : ^T Vector -> bool) (sf: ^T -> bool) (array: ^T[]) : int Option =

      checkNonNull array    

      let count = Vector< ^T>.Count
      let len = array.Length
      let mutable i = 0
      while i <= len-count && not (vf (Vector< ^T>(array,i))) do
          i <- i + count
    
      if i <= len-count then
          let v = Vector< ^T>(array,i)
          let mutable j = 0
          while j < count && not (sf v.[j]) do
              j <- j + 1    
              i <- i + 1    
          Some i                                          
      else    
          i <- len-len%count
          while i < array.Length && not (sf array.[i]) do
              i <- i + 1
          if i < len then
              Some i
          else
              None



  /// <summary>
  /// Takes a function that accepts a vector and returns true or false, and
  /// a function that takes a single element and returns true or false.
  /// Returns the Option value of the first element that satisfies both predicates
  /// or None if not found
  /// </summary>
  /// <param name="vf">Takes a Vector 'T and returns true or false</param>
  /// <param name="sf">Takes a 'T and returns true or false</param>
  /// <param name="array"></param>
  let inline tryFind
       (vf : ^T Vector -> bool) (sf: ^T -> bool) (array: ^T[]) : ^T Option =

     match tryFindIndex vf sf array with
     | Some i -> Some array.[i]
     | None -> None

  /// <summary>
  /// Takes a function that accepts a vector and returns true or false, and
  /// a function that takes a single element and returns true or false.
  /// Returns the Option index of the last element that satisfies both predicates
  /// or None if not found
  /// </summary>
  /// <param name="vf">Takes a Vector 'T and returns true or false</param>
  /// <param name="sf">Takes a 'T and returns true or false</param>
  /// <param name="array"></param>
  let inline tryFindIndexBack
       (vf : ^T Vector -> bool) (sf: ^T -> bool) (array: ^T[]) : int Option =

      checkNonNull array    

      let count = Vector< ^T>.Count
   
      let mutable i = array.Length-count
      while i >= 0 && not (vf (Vector< ^T>(array,i))) do
          i <- i - count
    
      if i >= 0 then
          let v = Vector< ^T>(array,i)
          i <- i + count - 1
          let mutable j = count-1
          while j >= 0 && not (sf v.[j]) do
              j <- j - 1                                    
              i <- i - 1
          Some i
      else    
          i <- i + count - 1
          while i >= 0 && not (sf array.[i]) do          
              i <- i - 1
          if i >= 0 then 
              Some i
          else
              None


  /// <summary>
  /// Takes a function that accepts a vector and returns true or false, and
  /// a function that takes a single element and returns true or false.
  /// Returns the Option value of the last element that satisfies both predicates
  /// or None if not found
  /// </summary>
  /// <param name="vf">Takes a Vector 'T and returns true or false</param>
  /// <param name="sf">Takes a 'T and returns true or false</param>
  /// <param name="array"></param>
  let inline tryFindBack
       (vf : ^T Vector -> bool) (sf: ^T -> bool) (array: ^T[]) : ^T Option =

     match tryFindIndexBack vf sf array with
     | Some i -> Some array.[i]
     | None -> None
          
  /// <summary>
  /// Checks for the existence of a value satisfying the Vector predicate. 
  /// </summary>
  /// <param name="f">Takes a Vector and returns true or false to indicate existence</param>
  /// <param name="array"></param>
  let inline exists 
      (vf : ^T Vector -> bool) 
      (sf : ^T -> bool)
      (array: ^T[]) : bool =
    
      checkNonNull array

      let count = Vector< ^T>.Count
      let len = array.Length
      let mutable found = false
        
      let mutable i = 0
      while i <= len-count do
          found <- vf (Vector< ^T>(array,i))
          if found then i <- len
          else i <- i + count


      i <- len-len%count
      while i < array.Length && not found do
          found <- sf array.[i]
          i <- i + 1

      found

  /// <summary>
  /// Checks if all Vectors satisfy the predicate.
  /// </summary>
  /// <param name="f">Takes a Vector and returns true or false</param>
  /// <param name="array"></param>
  let inline forall 
      (vf : ^T Vector -> bool) 
      (sf : ^T -> bool)
      (array: ^T[]) : bool =
    
      checkNonNull array

      let count = Vector< ^T>.Count
      let mutable found = true
      let len = array.Length
    
      let mutable i = 0
      while i <= len-count do
          found <- vf (Vector< ^T>(array,i))
          if not found then i <- len
          else i <- i + count

      i <- len-len%count
      while i < array.Length && found do
          found <- sf array.[i]
          i <- i + 1

      found


  /// <summary>
  /// Checks for the existence of a pair of values satisfying the Vector predicate. 
  /// </summary>
  /// <param name="f">Takes two Vectors and returns true or false to indicate existence</param>
  /// <param name="array"></param>
  let inline exists2 
      (vf : ^T Vector -> ^U Vector -> bool) 
      (sf : ^T -> ^U -> bool)
      (array1: ^T[]) (array2: ^U[]) : bool =
    
      checkNonNull array1
      checkNonNull array2

      let count = Vector< ^T>.Count
      if count <> Vector< ^U>.Count then invalidArg "array" "Arrays must have same Vector width"
    
      let len = array1.Length        
      if len <> array2.Length then invalidArg "array2" "Arrays must have same length"
             
      let mutable found = false
      let mutable i = 0
      while i <= len-count do
          found <- vf (Vector< ^T>(array1,i)) (Vector< ^U>(array2,i))
          if found then i <- len
          else i <- i + count

      i <- len-len%count
      while i < array1.Length && not found do
          found <- sf array1.[i] array2.[i]
          i <- i + 1

      found

  /// <summary>
  /// Checks for the if all Vector pairs satisfy the predicate
  /// </summary>
  /// <param name="f">Takes two Vectors and returns true or false to indicate existence</param>
  /// <param name="array"></param>
  let inline forall2 
      (vf : ^T Vector -> ^U Vector -> bool) 
      (sf : ^T -> ^U -> bool)
      (array1: ^T[]) 
      (array2: ^U[]) : bool =
    
      checkNonNull array1
      checkNonNull array2

      let count = Vector< ^T>.Count
      if count <> Vector< ^U>.Count then invalidArg "array" "Arrays must have same Vector width"
    
      let len = array1.Length        
      if len <> array2.Length then invalidArg "array2" "Arrays must have same length"
             
      let mutable found = true
      let mutable i = 0
      while i <= len-count do
          found <- vf (Vector< ^T>(array1,i)) (Vector< ^U>(array2,i))
          if not found then i <- len
          else i <- i + count

      i <- len-len%count
      while i < array1.Length && found do
          found <- sf array1.[i] array2.[i]
          i <- i + 1

      found


  /// <summary>
  /// Identical to the standard contains, just faster
  /// </summary>
  /// <param name="x"></param>
  /// <param name="array"></param>
  let inline contains (x : ^T) (array:^T[]) : bool =
    
      checkNonNull array

      let count = Vector< ^T>.Count      
      let len = array.Length    
      let compareVector = Vector< ^T>(x)    
    
      let mutable found = false
      let mutable i = 0
      while i <= len-count do
          found <- Vector.EqualsAny(Vector< ^T>(array,i),compareVector)
          if found then i <- len
          else i <- i + count

      i <- len-len%count
      while i < array.Length && not found do                
          found <- x = array.[i]
          i <- i + 1

      found


  /// <summary>
  /// Exactly like the standard Max function, only faster
  /// </summary>
  /// <param name="array"></param>
  let inline max (array :^T[]) : ^T =

      checkNonNull array

      let len = array.Length
      if len = 0 then invalidArg "array" "The input array was empty."
      let mutable max = array.[0]
      let count = Vector< ^T>.Count    

      let mutable i = 0
      if len >= count then
          let mutable maxV = Vector< ^T>(array,0)
          i <- i + count
          while i <= len-count do
             let v = Vector< ^T>(array,i)
             maxV <- Vector.Max(v,maxV)
             i <- i + count

          for j=0 to count-1 do
              if maxV.[j] > max then max <- maxV.[j]
    
      i <- len-len%count
      while i < array.Length do
          if array.[i] > max then max <- array.[i]
          i <- i + 1
      max

  /// <summary>
  /// Find the max by applying the function to each Vector in the array
  /// </summary>
  /// <param name="array"></param>
  let inline maxBy 
      (vf: Vector< ^T> -> Vector< ^U>) 
      (sf: ^T -> ^U)
      (array :^T[]) : ^U =
    
      checkNonNull array

      let len = array.Length
      if len = 0 then invalidArg "array" "The input array was empty."    
      let count = Vector< ^T>.Count
      
      let minValue = typeof< ^U>.GetTypeInfo().GetField("MinValue").GetValue() |> unbox< ^U>
      let mutable max = minValue 
      let mutable maxV =  Vector< ^U>(minValue)
      let mutable i = 0
      if len >= count then
          maxV  <- vf (Vector< ^T>(array,0))
          max <- maxV.[0]
          i <- i + count
          while i <= len-count do
              let v = vf (Vector< ^T>(array,i))
              maxV <- Vector.Max(v,maxV)
              i <- i + count                
    
      for j=0 to Vector< ^U>.Count-1 do
          if maxV.[j] > max then max <- maxV.[j]

      i <- len-len%count
      while i < array.Length do
          let x = sf array.[i]
          if x > max then max <- x
          i <- i + 1
    
      max

        
  /// <summary>
  /// Find the min by applying the function to each Vector in the array
  /// </summary>
  /// <param name="array"></param>
  let inline minBy 
      (vf: Vector< ^T> -> Vector< ^U>) 
      (sf: ^T -> ^U)
      (array :^T[]) : ^U =

      checkNonNull array
            
      let len = array.Length
      if len = 0 then invalidArg "array" "The input array was empty."    
      let count = Vector< ^T>.Count    
      let maxValue = typeof< ^U>.GetTypeInfo().GetField("MaxValue").GetValue() |> unbox< ^U>
      let mutable min = maxValue 
      let mutable minV =  Vector< ^U>(maxValue)
      let mutable i = 0
      if len >= count then
          minV  <- vf (Vector< ^T>(array,0))
          min <- minV.[0]
          i <- i + count
          while i <= len-count do
              let v = vf (Vector< ^T>(array,i))
              minV <- Vector.Min(v,minV)
              i <- i + count        
    
      for j=0 to Vector< ^U>.Count-1 do
          if minV.[j] < min then min <- minV.[j]

      i <- len-len%count
      while i < array.Length do
          let x = sf array.[i]
          if x < min then min <- x
          i <- i + 1
    
      min


  /// <summary>
  /// Exactly like the standard Min function, only faster
  /// </summary>
  /// <param name="array"></param>
  let inline min (array :^T[]) : ^T =

      checkNonNull array

      let len = array.Length
      if len = 0 then invalidArg "array" "empty array"
      let mutable min = array.[0]
      let count = Vector< ^T>.Count
    
      let mutable i = 0
      if len >= count then
          let mutable minV = Vector< ^T>(array,0)
          i <- i + count
          while i <= len-count do
              let v = Vector< ^T>(array,i)
              minV <- Vector.Min(v,minV)
              i <- i + count

          for j=0 to count-1 do
              if minV.[j] < min then min <- minV.[j]

      i <- len-len%count
      while i < array.Length do
          if array.[i] < min then min <- array.[i]
          i <- i + 1
      min

  /// <summary>
  /// Same as standard compareWith, but you proider a Vectorized comparer
  /// </summary>
  /// <param name="comparer">compares Vector chunks of each array</param>
  /// <param name="array1"></param>
  /// <param name="array2"></param>
  let inline compareWith (vf : Vector< ^T> -> Vector< ^U> -> int)     
                         (sf : ^T -> ^U -> int)                   
                         (array1: ^T[])
                         (array2: ^U[]) =

      checkNonNull array1
      checkNonNull array2
        
      let count = Vector< ^T>.Count
      if count <> Vector< ^U>.Count then invalidArg "array" "Inputs must all have same Vector width."
    
      let length1 = array1.Length
      let length2 = array2.Length
      let minLength = System.Math.Min(length1,length2)
            
      let mutable i = 0
      let mutable result = 0
            
    
      while i < minLength-count && (vf (Vector< ^T>(array1,i)) (Vector< ^U>(array2,i))) = 0 do                
          i <- i + count
    
      if result <> 0 then         
          result
      else        
          while i < minLength && result = 0 do
              result <- sf array1.[i] array2.[i]
              i <- i + 1
          if result <> 0 then
              result
          elif length1 = length2 then 0            
          elif length1 < length2 then -1
          else 1              
