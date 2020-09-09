Blake2b and xxHash used to be a part of Spreads.Core, but 
xxHash is not used and Blake2b is replaced by NuGet package [SauceControl.Blake2Fast](https://www.nuget.org/packages/SauceControl.Blake2Fast/) (Spreads used modified code from that project before).
Old implementations are kept in the test project for reference.