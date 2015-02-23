namespace Spreads

open System
open System.Collections
open System.Collections.Generic
open System.Linq
open System.Diagnostics
open System.Runtime.InteropServices
open Spreads

#nowarn "9"


// TODO this must be tweaked so that comparison could be done on int32/int64 and not on 
// each byte. 


[<StructLayoutAttribute(LayoutKind.Sequential)>]
type EntityId =
  struct
    val internal entityId : uint32
    internal new(entityId:uint32) = {entityId = entityId}
  end

[<StructLayoutAttribute(LayoutKind.Sequential)>]
type PropertyId =
  struct
    val internal propertyId : uint32
    internal new(propertyId:uint32) = {propertyId = propertyId}
  end

[<StructLayoutAttribute(LayoutKind.Sequential)>]
type PropertyLinkId =
  struct
    val internal of_ : uint16
    val internal in_ : byte
    val internal to_ : byte
    internal new(of_:uint16, in_:byte, to_:byte) = {of_ = of_;in_=in_;to_=to_}
  end

[<StructLayoutAttribute(LayoutKind.Sequential)>]
type MetricId =
  struct
    val internal scale : byte
    val internal nominator : byte
    val internal denominator : byte
    internal new(scale:byte, nominator:byte, denominator:byte) = {scale = scale;nominator=nominator;denominator=denominator}
  end

[<StructLayoutAttribute(LayoutKind.Sequential)>]
type EpochId =
  struct
    val internal epochId : byte
    internal new(epochId:byte) = {epochId = epochId}
  end


[<StructLayoutAttribute(LayoutKind.Sequential)>]
type SeriesId =
  struct
    val internal entityId : EntityId
    val internal propertyId : PropertyId
    val internal propertyLinkId : PropertyLinkId
    val internal metricId : MetricId
    val internal epochId : EpochId
    internal new(entityId:EntityId,
                  propertyId : PropertyId,
                  propertyLinkId : PropertyLinkId,
                  metricId : MetricId,
                  epochId : EpochId) = {entityId=entityId;
                                        propertyId = propertyId;
                                        propertyLinkId = propertyLinkId;
                                        metricId = metricId;
                                        epochId = epochId}
  end

[<StructLayoutAttribute(LayoutKind.Sequential)>]
type SeriesBucketId =
  struct
    val internal seriesId : SeriesId
    val internal timePeriod : TimePeriod
    internal new(seriesId:SeriesId, timePeriod : TimePeriod) = 
      {seriesId=seriesId; timePeriod = timePeriod}
  end