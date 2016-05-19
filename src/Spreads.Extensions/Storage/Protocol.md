Spreads replication protocol
============================


Command Header
------
Command message header has three fields:

    internal struct CommandHeader {
            public UUID SeriesId;
            public CommandType CommandType;
            public long Version;
    }




* SeriesId is MD5 hash of UTF8 bytes of series text id.
* CommandType is enum:


        public enum CommandType : int {
            Set = 0,
            Complete = 10,
            Remove = 20,
            Append = 30,
            SetChunk = 40,
            RemoveChunk = 50,
            Subscribe = 60,
            Flush = 70,
            AcquireLock = 80,
            ReleaseLock = 90,
        }

* Version is a series current version or a version that is effective **after** a mutation command is applied, e.g.
after set/complete/remove/append operations (other commands do not change series version).


Open read-only series by series text id: `ReadSeries<K,V>(seriesTextId)`
---------------------
* Send Subscribe command with UUID as MD5 hash of UTF8 bytes of series text id.
* 


Read-only

Subscribe (current version)
==> Flush (all)