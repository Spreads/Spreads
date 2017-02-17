// The MIT License (MIT)
//
// Copyright (c) 2016 Jack Mott
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.


namespace Spreads
module SIMDArrayUtils =

  /// <summary>
  /// Utility function for use with SIMD higher order functions
  /// When you don't have leftover elements
  /// example:
  /// Array.SIMD.Map (fun x -> x*x) nop array
  /// Where array is divisible by your SIMD width or you don't
  /// care about what happens to the leftover elements
  /// </summary>
  let inline nop _ = Unchecked.defaultof<_>

  let inline checkNonNull arg =
      match box arg with
      | null -> nullArg "array"
      | _ -> ()


  open System.Threading.Tasks
  open System

  let inline private applyTask fromInc toExc stride f = 
         // printf "fromIncA:%A toExcA:%A stride:%A\n" fromInc toExc stride
          let mutable i = fromInc
          while i < toExc do
              f i
              i <- i + stride

  let inline private applyTaskAggregate fromInc toExc stride acc f : ^T = 
         // printf "fromIncA:%A toExcA:%A stride:%A\n" fromInc toExc stride
          let mutable i = fromInc
          let mutable acc = acc
          while i < toExc do
              acc <- f i acc
              i <- i + stride
          acc


  let inline ForStride (fromInclusive : int) (toExclusive :int) (stride : int) (f : int -> unit) =
            
      let numStrides = (toExclusive-fromInclusive)/stride
      if numStrides > 0 then
          let numTasks = Math.Min(Environment.ProcessorCount,numStrides)
          let stridesPerTask = numStrides/numTasks
          let elementsPerTask = stridesPerTask * stride;
          let mutable remainderStrides = numStrides - (stridesPerTask*numTasks)
    
          //printf "len:%A numTasks:%A numStrides:%A stridesPerCore:%A elementsPerCore:%A remainderStrides:%A\n" len numTasks numStrides stridesPerTask elementsPerTask remainderStrides    
          let taskArray : Task[] = Array.zeroCreate numTasks
          let mutable index = 0    
          for i = 0 to taskArray.Length-1 do        
              let toExc =
                  if remainderStrides = 0 then
                      index + elementsPerTask
                  else
                      remainderStrides <- remainderStrides - 1
                      index + elementsPerTask + stride
              let fromInc = index;
              //printf "index:%A toExc:%A\n" index toExc
        
              taskArray.[i] <- Task.Factory.StartNew(fun () -> applyTask fromInc toExc stride f)                        
              index <- toExc
                        
          Task.WaitAll(taskArray)


  let inline ForStrideAggreagate (fromInclusive : int) (toExclusive :int) (stride : int) (acc: ^T) (f : int -> ^T -> ^T) combiner =      
      let numStrides = (toExclusive-fromInclusive)/stride
      if numStrides > 0 then
          let numTasks = Math.Min(Environment.ProcessorCount,numStrides)
          let stridesPerTask = numStrides/numTasks
          let elementsPerTask = stridesPerTask * stride;
          let mutable remainderStrides = numStrides - (stridesPerTask*numTasks)
    
        //  printf "numTasks:%A numStrides:%A stridesPerCore:%A elementsPerCore:%A remainderStrides:%A\n" numTasks numStrides stridesPerTask elementsPerTask remainderStrides    
          let taskArray : Task< ^T>[] = Array.zeroCreate numTasks
          let mutable index = 0    
          for i = 0 to taskArray.Length-1 do        
              let toExc =
                  if remainderStrides = 0 then
                      index + elementsPerTask
                  else
                      remainderStrides <- remainderStrides - 1
                      index + elementsPerTask + stride
              let fromInc = index;
              //printf "index:%A toExc:%A\n" index toExc
              let acc = acc
              taskArray.[i] <- Task< ^T>.Factory.StartNew(fun () -> applyTaskAggregate fromInc toExc stride acc f)                        
              index <- toExc
                        
          let mutable result = acc
          for i = 0 to taskArray.Length-1 do       
              result <- combiner result taskArray.[i].Result    
          result
      else
          acc
