@echo off
for /f "tokens=2 delims==" %%a in ('wmic OS Get localdatetime /value') do set "dt=%%a"
set "YY=%dt:~2,2%" & set "YYYY=%dt:~0,4%" & set "MM=%dt:~4,2%" & set "DD=%dt:~6,2%"
set "HH=%dt:~8,2%" & set "Min=%dt:~10,2%" & set "Sec=%dt:~12,2%"

REM set "datestamp=%YYYY%%MM%%DD%" & set "timestamp=%HH%%Min%%Sec%"
set "fullstamp=%YY%%MM%%DD%%HH%%Min%"
REM echo datestamp: "%datestamp%"
REM echo timestamp: "%timestamp%"
REM echo fullstamp: "%fullstamp%"

set "build=build%fullstamp%"

call ../tools/re_write_il ../bin/net451/ ../bin/net451/Spreads.Collections.dll ../tools/SpreadsKey.snk
call ../tools/re_write_il ../bin/netstandard2.0/ ../bin/netstandard2.0/Spreads.Collections.dll ../tools/SpreadsKey.snk