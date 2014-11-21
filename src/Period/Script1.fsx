
open System
open System.Collections.Generic

let tz = TimeZoneInfo.Local
let offs = List<int>()

for i in 0..364 do
  let dt = DateTime(2014, 1, 1).AddDays(float i)
  let off = tz.GetUtcOffset(dt).Hours
  Console.WriteLine(dt.ToString() + " : " + off.ToString())
  offs.Add(tz.GetUtcOffset(dt).Hours)

offs


#time "on"
let dt = DateTime.UtcNow
for i in 0..1000000 do
  let year = dt.Year
  let month = dt.Month
  let day = dt.Day
  let tick = dt.TimeOfDay.Ticks
  let dtn = DateTime(year, month,day).AddTicks(tick)
  if dt <> dtn then failwith "wrong"
  ()

for i in 0..1000000 do
  let d = DateTime(1900, 1, 1).AddMonths(1049)
  ()