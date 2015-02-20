namespace Spreads

type ISerializer =
    abstract Serialize: 'T -> byte[]
    abstract Deserialize: byte[] -> 'T
    abstract Deserialize: byte[] * System.Type -> obj