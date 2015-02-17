namespace Spreads

type ISerializer =
    abstract Serialize: 'T -> byte[]
    abstract Deserialize: byte[] -> 'T
