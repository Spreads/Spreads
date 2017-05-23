// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

namespace Spreads

open System
open System.Collections.Generic

type KVP<'K,'V> = KeyValuePair<'K,'V>

type DummyDisposable private() =
  static let instance = new DummyDisposable()
  interface IDisposable with
    member x.Dispose() = ()
  static member Instance with get() = instance :> IDisposable