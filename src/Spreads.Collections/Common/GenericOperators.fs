// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


namespace Spreads

open System
open System.Linq.Expressions
open System.Collections.Generic


 
/// Dynamic (runtime) generic operators allow to add/subtract/etc anything that supports an operation
/// They support boxed value types, e.g. Op.Add(box 1, box 2) or untypes interfaces, 
/// e.g. Op.Add(l:>ISeries, r:>ISeries)  (of true type ISeries<'K,'V>)
/// will be resolved to the operator defined on ISeries<'K,'V>'s implementation
/// N.B. This stuff is slow for numbers and should be used only as "optimistic replacement for 
/// reflection", when a result is expected to succeed, but type inference doesn't know about that.

type internal Op()=
    
  static member private negateDelegates = // !
      (Dictionary<Type, Delegate>(HashIdentity.Structural)) 
  static member Negate(one : obj) = // !
      let oty = one.GetType()
      let dlg = 
          let suc, res = Op.negateDelegates.TryGetValue(oty) // !
          if suc then
              res
          else
              let pO = Expression.Parameter(oty, "one")
              let body = Expression.Negate(pO) // ! 
              let lambda = Expression.Lambda(body, pO).Compile()
              Op.negateDelegates.[oty] <- lambda // !
              lambda
      dlg.DynamicInvoke([|one|])


  static member private addDelegates = // !
      (Dictionary<Type * Type, Delegate>(HashIdentity.Structural)) 
  static member Add(left, right) =
      let lty = left.GetType()
      let rty = right.GetType()
      let dlg = 
          let suc, res = Op.addDelegates.TryGetValue((lty, rty))
          if suc then
              res
          else
              let pL = Expression.Parameter(lty, "left")
              let pR = Expression.Parameter(rty, "right")
              let body = Expression.Add(pL, pR) // !
              let lambda = Expression.Lambda(body, pL, pR).Compile()
              Op.addDelegates.[(lty, rty)] <- lambda
              lambda
      dlg.DynamicInvoke(left, right)

  static member private subtractDelegates = // !
      (Dictionary<Type * Type, Delegate>(HashIdentity.Structural)) 
  static member Subtract(left, right) = // !
      let lty = left.GetType()
      let rty = right.GetType()
      let dlg = 
          let suc, res = Op.subtractDelegates.TryGetValue((lty, rty)) // !
          if suc then
              res
          else
              let pL = Expression.Parameter(lty, "left")
              let pR = Expression.Parameter(rty, "right")
              let body = Expression.Subtract(pL, pR) // ! 
              let lambda = Expression.Lambda(body, pL, pR).Compile()
              Op.subtractDelegates.[(lty, rty)] <- lambda // !
              lambda
      dlg.DynamicInvoke(left, right)

  static member private multiplyDelegates = // !
      (Dictionary<Type * Type, Delegate>(HashIdentity.Structural)) 
  static member Multiply(left, right) = // !
      let lty = left.GetType()
      let rty = right.GetType()
      let dlg = 
          let suc, res = Op.multiplyDelegates.TryGetValue((lty, rty)) // !
          if suc then
              res
          else
              let pL = Expression.Parameter(lty, "left")
              let pR = Expression.Parameter(rty, "right")
              let body = Expression.Multiply(pL, pR) // ! 
              let lambda = Expression.Lambda(body, pL, pR).Compile()
              Op.multiplyDelegates.[(lty, rty)] <- lambda // !
              lambda
      dlg.DynamicInvoke(left, right)

  static member private divideDelegates = // !
      (Dictionary<Type * Type, Delegate>(HashIdentity.Structural)) 
  static member Divide(left, right) = // !
      let lty = left.GetType()
      let rty = right.GetType()
      let dlg = 
          let suc, res = Op.divideDelegates.TryGetValue((lty, rty)) // !
          if suc then
              res
          else
              let pL = Expression.Parameter(lty, "left")
              let pR = Expression.Parameter(rty, "right")
              let body = Expression.Divide(pL, pR) // ! 
              let lambda = Expression.Lambda(body, pL, pR).Compile()
              Op.divideDelegates.[(lty, rty)] <- lambda // !
              lambda
      dlg.DynamicInvoke(left, right)

  static member private powerDelegates = // !
      (Dictionary<Type * Type, Delegate>(HashIdentity.Structural)) 
  static member Power(left, right) = // !
      let lty = left.GetType()
      let rty = right.GetType()
      let dlg = 
          let suc, res = Op.powerDelegates.TryGetValue((lty, rty)) // !
          if suc then
              res
          else
              let pL = Expression.Parameter(lty, "left")
              let pR = Expression.Parameter(rty, "right")
              let body = Expression.Power(pL, pR) // ! 
              let lambda = Expression.Lambda(body, pL, pR).Compile()
              Op.powerDelegates.[(lty, rty)] <- lambda // !
              lambda
      dlg.DynamicInvoke(left, right)

  static member private moduloDelegates = // !
      (Dictionary<Type * Type, Delegate>(HashIdentity.Structural)) 
  static member Modulo(left, right) = // !
      let lty = left.GetType()
      let rty = right.GetType()
      let dlg = 
          let suc, res = Op.moduloDelegates.TryGetValue((lty, rty)) // !
          if suc then
              res
          else
              let pL = Expression.Parameter(lty, "left")
              let pR = Expression.Parameter(rty, "right")
              let body = Expression.Modulo(pL, pR) // ! 
              let lambda = Expression.Lambda(body, pL, pR).Compile()
              Op.moduloDelegates.[(lty, rty)] <- lambda // !
              lambda
      dlg.DynamicInvoke(left, right)




  static member private andDelegates = // !
      (Dictionary<Type * Type, Delegate>(HashIdentity.Structural)) 
  static member And(left, right) = // !
      let lty = left.GetType()
      let rty = right.GetType()
      let dlg = 
          let suc, res = Op.andDelegates.TryGetValue((lty, rty)) // !
          if suc then
              res
          else
              let pL = Expression.Parameter(lty, "left")
              let pR = Expression.Parameter(rty, "right")
              let body = Expression.And(pL, pR) // ! 
              let lambda = Expression.Lambda(body, pL, pR).Compile()
              Op.andDelegates.[(lty, rty)] <- lambda // !
              lambda
      dlg.DynamicInvoke(left, right)

  static member private orDelegates = // !
      (Dictionary<Type * Type, Delegate>(HashIdentity.Structural)) 
  static member Or(left, right) = // !
      let lty = left.GetType()
      let rty = right.GetType()
      let dlg = 
          let suc, res = Op.orDelegates.TryGetValue((lty, rty)) // !
          if suc then
              res
          else
              let pL = Expression.Parameter(lty, "left")
              let pR = Expression.Parameter(rty, "right")
              let body = Expression.Or(pL, pR) // ! 
              let lambda = Expression.Lambda(body, pL, pR).Compile()
              Op.orDelegates.[(lty, rty)] <- lambda // !
              lambda
      dlg.DynamicInvoke(left, right)

  static member private xorDelegates = // !
      (Dictionary<Type * Type , Delegate>(HashIdentity.Structural)) 
  static member Xor(left, right) = // !
      let lty = left.GetType()
      let rty = right.GetType()
      let dlg = 
          let suc, res = Op.xorDelegates.TryGetValue((lty, rty)) // !
          if suc then
              res
          else
              let pL = Expression.Parameter(lty, "left")
              let pR = Expression.Parameter(rty, "right")
              let body = Expression.ExclusiveOr(pL, pR) // ! 
              let lambda = Expression.Lambda(body, pL, pR).Compile()
              Op.xorDelegates.[(lty, rty)] <- lambda // !
              lambda
      dlg.DynamicInvoke(left, right)

  static member private equalDelegates = // !
      (Dictionary<Type * Type , Delegate>(HashIdentity.Structural)) 
  static member Equal(left, right) : bool = // !
      let lty = left.GetType()
      let rty = right.GetType()
      let dlg = 
          let suc, res = Op.equalDelegates.TryGetValue((lty, rty)) // !
          if suc then
              res
          else
              let pL = Expression.Parameter(lty, "left")
              let pR = Expression.Parameter(rty, "right")
              let body = Expression.Equal(pL, pR) // ! 
              let lambda = Expression.Lambda(body, pL, pR).Compile()
              Op.equalDelegates.[(lty, rty)] <- lambda // !
              lambda
      dlg.DynamicInvoke(left, right) :?> bool

  static member private notEqualDelegates = // !
      (Dictionary<Type * Type , Delegate>(HashIdentity.Structural)) 
  static member NotEqual(left, right) : bool = // !
      let lty = left.GetType()
      let rty = right.GetType()
      let dlg = 
          let suc, res = Op.notEqualDelegates.TryGetValue((lty, rty)) // !
          if suc then
              res
          else
              let pL = Expression.Parameter(lty, "left")
              let pR = Expression.Parameter(rty, "right")
              let body = Expression.NotEqual(pL, pR) // ! 
              let lambda = Expression.Lambda(body, pL, pR).Compile()
              Op.notEqualDelegates.[(lty, rty)] <- lambda // !
              lambda
      dlg.DynamicInvoke(left, right) :?> bool


  static member private lessThanDelegates = // !
      (Dictionary<Type * Type , Delegate>(HashIdentity.Structural)) 
  static member LessThan(left, right) : bool = // !
      let lty = left.GetType()
      let rty = right.GetType()
      let dlg = 
          let suc, res = Op.lessThanDelegates.TryGetValue((lty, rty)) // !
          if suc then
              res
          else
              let pL = Expression.Parameter(lty, "left")
              let pR = Expression.Parameter(rty, "right")
              let body = Expression.LessThan(pL, pR) // ! 
              let lambda = Expression.Lambda(body, pL, pR).Compile()
              Op.lessThanDelegates.[(lty, rty)] <- lambda // !
              lambda
      dlg.DynamicInvoke(left, right) :?> bool
        

  static member private lessThanOrEqualDelegates = // !
      (Dictionary<Type * Type , Delegate>(HashIdentity.Structural)) 
  static member LessThanOrEqual(left, right) : bool = // !
      let lty = left.GetType()
      let rty = right.GetType()
      let dlg = 
          let suc, res = Op.lessThanOrEqualDelegates.TryGetValue((lty, rty)) // !
          if suc then
              res
          else
              let pL = Expression.Parameter(lty, "left")
              let pR = Expression.Parameter(rty, "right")
              let body = Expression.LessThanOrEqual(pL, pR) // ! 
              let lambda = Expression.Lambda(body, pL, pR).Compile()
              Op.lessThanOrEqualDelegates.[(lty, rty)] <- lambda // !
              lambda
      dlg.DynamicInvoke(left, right) :?> bool

  static member private greaterThanDelegates = // !
      (Dictionary<Type * Type , Delegate>(HashIdentity.Structural)) 
  static member GreaterThan(left, right) : bool = // !
      let lty = left.GetType()
      let rty = right.GetType()
      let dlg = 
          let suc, res = Op.greaterThanDelegates.TryGetValue((lty, rty)) // !
          if suc then
              res
          else
              let pL = Expression.Parameter(lty, "left")
              let pR = Expression.Parameter(rty, "right")
              let body = Expression.GreaterThan(pL, pR) // ! 
              let lambda = Expression.Lambda(body, pL, pR).Compile()
              Op.greaterThanDelegates.[(lty, rty)] <- lambda // !
              lambda
      dlg.DynamicInvoke(left, right) :?> bool

  static member private greaterThanOrEqualDelegates = // !
      (Dictionary<Type * Type , Delegate>(HashIdentity.Structural)) 
  static member GreaterThanOrEqual(left, right) : bool = // !
      let lty = left.GetType()
      let rty = right.GetType()
      let dlg = 
          let suc, res = Op.greaterThanOrEqualDelegates.TryGetValue((lty, rty)) // !
          if suc then
              res
          else
              let pL = Expression.Parameter(lty, "left")
              let pR = Expression.Parameter(rty, "right")
              let body = Expression.GreaterThanOrEqual(pL, pR) // ! 
              let lambda = Expression.Lambda(body, pL, pR).Compile()
              Op.greaterThanOrEqualDelegates.[(lty, rty)] <- lambda // !
              lambda
      dlg.DynamicInvoke(left, right) :?> bool



type Op<'T>()=
    
  static let negateDelegate = lazy (
      let pO = Expression.Parameter(typeof<'T>, "one")
      let body = Expression.Negate(pO)
      Expression.Lambda<Func<'T,'T>>(body, pO).Compile()
      )

  static let addDelegate = lazy (
      let pL = Expression.Parameter(typeof<'T>, "left")
      let pR = Expression.Parameter(typeof<'T>, "right")
      let body = Expression.Add(pL, pR)
      Expression.Lambda<Func<'T,'T,'T>>(body, pL, pR).Compile()
      )
  static let subtractDelegate = lazy (
      let pL = Expression.Parameter(typeof<'T>, "left")
      let pR = Expression.Parameter(typeof<'T>, "right")
      let body = Expression.Subtract(pL, pR)
      Expression.Lambda<Func<'T,'T,'T>>(body, pL, pR).Compile()
      )
  static let multiplyDelegate = lazy ( 
      let pL = Expression.Parameter(typeof<'T>, "left")
      let pR = Expression.Parameter(typeof<'T>, "right")
      let body = Expression.Multiply(pL, pR)
      Expression.Lambda<Func<'T,'T,'T>>(body, pL, pR).Compile()
      )
  static let divideDelegate = lazy ( 
      let pL = Expression.Parameter(typeof<'T>, "left")
      let pR = Expression.Parameter(typeof<'T>, "right")
      let body = Expression.Divide(pL, pR)
      Expression.Lambda<Func<'T,'T,'T>>(body, pL, pR).Compile()
      )
  static let powerDelegate = lazy ( 
      let pL = Expression.Parameter(typeof<'T>, "left")
      let pR = Expression.Parameter(typeof<'T>, "right")
      let body = Expression.Power(pL, pR)
      Expression.Lambda<Func<'T,'T,'T>>(body, pL, pR).Compile()
      )
  static let moduloDelegate = lazy ( 
      let pL = Expression.Parameter(typeof<'T>, "left")
      let pR = Expression.Parameter(typeof<'T>, "right")
      let body = Expression.Modulo(pL, pR)
      Expression.Lambda<Func<'T,'T,'T>>(body, pL, pR).Compile()
      )
  static let equalDelegate = lazy ( 
      let pL = Expression.Parameter(typeof<'T>, "left")
      let pR = Expression.Parameter(typeof<'T>, "right")
      let body = Expression.Equal(pL, pR)
      Expression.Lambda<Func<'T,'T,'T>>(body, pL, pR).Compile()
      )
  static let notEqualDelegate = lazy (
      let pL = Expression.Parameter(typeof<'T>, "left")
      let pR = Expression.Parameter(typeof<'T>, "right")
      let body = Expression.NotEqual(pL, pR)
      Expression.Lambda<Func<'T,'T,'T>>(body, pL, pR).Compile()
      )
  static let lessThanDelegates = lazy ( 
      let pL = Expression.Parameter(typeof<'T>, "left")
      let pR = Expression.Parameter(typeof<'T>, "right")
      let body = Expression.LessThan(pL, pR)
      Expression.Lambda<Func<'T,'T,'T>>(body, pL, pR).Compile()
      )
  static let lessThanOrEqualDelegate = lazy ( 
      let pL = Expression.Parameter(typeof<'T>, "left")
      let pR = Expression.Parameter(typeof<'T>, "right")
      let body = Expression.LessThanOrEqual(pL, pR)
      Expression.Lambda<Func<'T,'T,'T>>(body, pL, pR).Compile()
      )
  static let greaterThanDelegate = lazy ( 
      let pL = Expression.Parameter(typeof<'T>, "left")
      let pR = Expression.Parameter(typeof<'T>, "right")
      let body = Expression.GreaterThan(pL, pR)
      Expression.Lambda<Func<'T,'T,'T>>(body, pL, pR).Compile()
      )
  static let greaterThanOrEqualDelegate = lazy ( 
      let pL = Expression.Parameter(typeof<'T>, "left")
      let pR = Expression.Parameter(typeof<'T>, "right")
      let body = Expression.GreaterThanOrEqual(pL, pR)
      Expression.Lambda<Func<'T,'T,'T>>(body, pL, pR).Compile()
      )
  static member Negate(one : 'T)      = negateDelegate.Value.Invoke(one)
  static member Add(left, right)      = addDelegate.Value.Invoke(left, right)
  static member Subtract(left, right) = subtractDelegate.Value.Invoke(left, right)
  static member Multiply(left, right) = multiplyDelegate.Value.Invoke(left, right)
  static member Divide(left, right)   = divideDelegate.Value.Invoke(left, right)
  static member Power(left, right)    = powerDelegate.Value.Invoke(left, right)
  static member Modulo(left, right)   = moduloDelegate.Value.Invoke(left, right)
  static member EQ(left, right)       = equalDelegate.Value.Invoke(left, right)
  static member NEQ(left, right)      = notEqualDelegate.Value.Invoke(left, right)
  static member LT(left, right)       = lessThanDelegates.Value.Invoke(left, right)
  static member LE(left, right)       = lessThanOrEqualDelegate.Value.Invoke(left, right)
  static member GT(left, right)       = greaterThanDelegate.Value.Invoke(left, right)
  static member GE(left, right)       = greaterThanOrEqualDelegate.Value.Invoke(left, right)


type Op<'T1,'T2,'T3>()=
    
  static let addDelegate = lazy ( 
      let pL = Expression.Parameter(typeof<'T1>, "left")
      let pR = Expression.Parameter(typeof<'T2>, "right")
      let body = Expression.Add(pL, pR)
      Expression.Lambda<Func<'T1,'T2,'T3>>(body, pL, pR).Compile()
      )
  static let subtractDelegate = lazy (  
      let pL = Expression.Parameter(typeof<'T1>, "left")
      let pR = Expression.Parameter(typeof<'T2>, "right")
      let body = Expression.Subtract(pL, pR)
      Expression.Lambda<Func<'T1,'T2,'T3>>(body, pL, pR).Compile()
      )
  static let multiplyDelegate = lazy (  
      let pL = Expression.Parameter(typeof<'T1>, "left")
      let pR = Expression.Parameter(typeof<'T2>, "right")
      let body = Expression.Multiply(pL, pR)
      Expression.Lambda<Func<'T1,'T2,'T3>>(body, pL, pR).Compile()
      )
  static let divideDelegate = lazy (  
      let pL = Expression.Parameter(typeof<'T1>, "left")
      let pR = Expression.Parameter(typeof<'T2>, "right")
      let body = Expression.Divide(pL, pR)
      Expression.Lambda<Func<'T1,'T2,'T3>>(body, pL, pR).Compile()
      )
  static let powerDelegate = lazy (  
      let pL = Expression.Parameter(typeof<'T1>, "left")
      let pR = Expression.Parameter(typeof<'T2>, "right")
      let body = Expression.Power(pL, pR)
      Expression.Lambda<Func<'T1,'T2,'T3>>(body, pL, pR).Compile()
      )
  static let moduloDelegate = lazy (  
      let pL = Expression.Parameter(typeof<'T1>, "left")
      let pR = Expression.Parameter(typeof<'T2>, "right")
      let body = Expression.Modulo(pL, pR)
      Expression.Lambda<Func<'T1,'T2,'T3>>(body, pL, pR).Compile()
      )
  static let equalDelegate = lazy (  
      let pL = Expression.Parameter(typeof<'T1>, "left")
      let pR = Expression.Parameter(typeof<'T2>, "right")
      let body = Expression.Equal(pL, pR)
      Expression.Lambda<Func<'T1,'T2,'T3>>(body, pL, pR).Compile()
      )
  static let notEqualDelegate = lazy (  
      let pL = Expression.Parameter(typeof<'T1>, "left")
      let pR = Expression.Parameter(typeof<'T2>, "right")
      let body = Expression.NotEqual(pL, pR)
      Expression.Lambda<Func<'T1,'T2,'T3>>(body, pL, pR).Compile()
      )
  static let lessThanDelegates = lazy (  
      let pL = Expression.Parameter(typeof<'T1>, "left")
      let pR = Expression.Parameter(typeof<'T2>, "right")
      let body = Expression.LessThan(pL, pR)
      Expression.Lambda<Func<'T1,'T2,'T3>>(body, pL, pR).Compile()
      )
  static let lessThanOrEqualDelegate = lazy (  
      let pL = Expression.Parameter(typeof<'T1>, "left")
      let pR = Expression.Parameter(typeof<'T2>, "right")
      let body = Expression.LessThanOrEqual(pL, pR)
      Expression.Lambda<Func<'T1,'T2,'T3>>(body, pL, pR).Compile()
      )
  static let greaterThanDelegate = lazy (  
      let pL = Expression.Parameter(typeof<'T1>, "left")
      let pR = Expression.Parameter(typeof<'T2>, "right")
      let body = Expression.GreaterThan(pL, pR)
      Expression.Lambda<Func<'T1,'T2,'T3>>(body, pL, pR).Compile()
      )
  static let greaterThanOrEqualDelegate = lazy (  
      let pL = Expression.Parameter(typeof<'T1>, "left")
      let pR = Expression.Parameter(typeof<'T2>, "right")
      let body = Expression.GreaterThanOrEqual(pL, pR)
      Expression.Lambda<Func<'T1,'T2,'T3>>(body, pL, pR).Compile()
      )

  static member Add(left, right)      = addDelegate.Value.Invoke(left, right)
  static member Subtract(left, right) = subtractDelegate.Value.Invoke(left, right)
  static member Multiply(left, right) = multiplyDelegate.Value.Invoke(left, right)
  static member Divide(left, right)   = divideDelegate.Value.Invoke(left, right)
  static member Power(left, right)    = powerDelegate.Value.Invoke(left, right)
  static member Modulo(left, right)   = moduloDelegate.Value.Invoke(left, right)
  static member EQ(left, right)       = equalDelegate.Value.Invoke(left, right)
  static member NEQ(left, right)      = notEqualDelegate.Value.Invoke(left, right)
  static member LT(left, right)       = lessThanDelegates.Value.Invoke(left, right)
  static member LE(left, right)       = lessThanOrEqualDelegate.Value.Invoke(left, right)
  static member GT(left, right)       = greaterThanDelegate.Value.Invoke(left, right)
  static member GE(left, right)       = greaterThanOrEqualDelegate.Value.Invoke(left, right)

