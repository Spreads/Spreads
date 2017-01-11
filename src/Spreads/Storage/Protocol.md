Spreads protocol (WIP draft)
================

Conductor is a service that replies to DataRepo requests. It is optional - when absent,
one of DataRepo instances acts as a conductor. In this case it assumes that local
storage has all data (without a service connected to upstream we cannot get additional data anyway)

Steps

0. If Writer, acquire global write lock. If not conductor:
    * Send write lock request with the current Pid.
    * Wait for NewWriter response.
    * If NewWriter response has the same Pid and the current Pid, lock is acquired, otherwise throw `InvalidOperationException`
1. Subscribe.
    * Send subscribe request with the current series version available locally.
    * Wait for `Flush` on this series.
    * Accumulate updates if any
    * Process responses with missed data until there are no more data (hasMore)


