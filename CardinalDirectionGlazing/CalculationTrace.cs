using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace CardinalDirectionGlazing
{
    [DataContract]
    public sealed class CalculationTrace
    {
        public CalculationTrace()
        {
            Targets = new List<TargetTrace>();
        }

        public CalculationTrace(string buildVersion, string mode)
            : this()
        {
            BuildVersion = buildVersion;
            Mode = mode;
            TimestampUtc = DateTime.UtcNow.ToString("o");
        }

        [DataMember(Order = 1)]
        public string? TimestampUtc { get; set; }

        [DataMember(Order = 2)]
        public string? BuildVersion { get; set; }

        [DataMember(Order = 3)]
        public string? Mode { get; set; }

        [DataMember(Order = 4)]
        public List<TargetTrace> Targets { get; set; }

        public TargetTrace StartTarget(string uniqueId)
        {
            var target = new TargetTrace(uniqueId);
            Targets.Add(target);
            return target;
        }
    }

    [DataContract]
    public sealed class TargetTrace
    {
        public TargetTrace()
        {
            Sources = new List<SourceTrace>();
        }

        public TargetTrace(string uniqueId)
            : this()
        {
            UniqueId = uniqueId;
        }

        [DataMember(Order = 1)]
        public string? ElementId { get; set; }

        [DataMember(Order = 2)]
        public string? UniqueId { get; set; }

        [DataMember(Order = 3)]
        public string? Number { get; set; }

        [DataMember(Order = 4)]
        public string? Name { get; set; }

        [DataMember(Order = 5)]
        public string? Outcome { get; set; }

        [DataMember(Order = 6)]
        public string? ReasonCode { get; set; }

        [DataMember(Order = 7)]
        public List<SourceTrace> Sources { get; set; }

        public SourceTrace StartSource(string sourceType, string uniqueId)
        {
            var source = new SourceTrace(sourceType, uniqueId);
            Sources.Add(source);
            return source;
        }

        public void Complete(string outcome, string reasonCode)
        {
            Outcome = outcome;
            ReasonCode = reasonCode;
        }
    }

    [DataContract]
    public sealed class SourceTrace
    {
        public SourceTrace()
        {
            Steps = new List<TraceStep>();
        }

        public SourceTrace(string sourceType, string uniqueId)
            : this()
        {
            SourceType = sourceType;
            UniqueId = uniqueId;
        }

        [DataMember(Order = 1)]
        public string? SourceType { get; set; }

        [DataMember(Order = 2)]
        public string? ElementId { get; set; }

        [DataMember(Order = 3)]
        public string? UniqueId { get; set; }

        [DataMember(Order = 4)]
        public string? Outcome { get; set; }

        [DataMember(Order = 5)]
        public string? ReasonCode { get; set; }

        [DataMember(Order = 6)]
        public List<TraceStep> Steps { get; set; }

        [DataMember(Order = 7)]
        public DirectionTrace? Direction { get; set; }

        public TraceStep StartStep(string name)
        {
            var step = new TraceStep(name);
            Steps.Add(step);
            return step;
        }

        public void Complete(string outcome, string reasonCode)
        {
            Outcome = outcome;
            ReasonCode = reasonCode;
        }
    }

    [DataContract]
    public sealed class TraceStep
    {
        public TraceStep()
        {
            Points = new List<TracePoint>();
        }

        public TraceStep(string name)
            : this()
        {
            Name = name;
        }

        [DataMember(Order = 1)]
        public string? Name { get; set; }

        [DataMember(Order = 2)]
        public string? Outcome { get; set; }

        [DataMember(Order = 3)]
        public string? ReasonCode { get; set; }

        [DataMember(Order = 4)]
        public List<TracePoint> Points { get; set; }
    }

    [DataContract]
    public sealed class TracePoint
    {
        public TracePoint()
        {
        }

        public TracePoint(string name, double x, double y, double z)
        {
            Name = name;
            X = x;
            Y = y;
            Z = z;
        }

        [DataMember(Order = 1)]
        public string? Name { get; set; }

        [DataMember(Order = 2)]
        public double X { get; set; }

        [DataMember(Order = 3)]
        public double Y { get; set; }

        [DataMember(Order = 4)]
        public double Z { get; set; }
    }

    [DataContract]
    public sealed class TraceVector
    {
        public TraceVector()
        {
        }

        public TraceVector(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        [DataMember(Order = 1)]
        public double X { get; set; }

        [DataMember(Order = 2)]
        public double Y { get; set; }

        [DataMember(Order = 3)]
        public double Z { get; set; }
    }

    [DataContract]
    public sealed class DirectionTrace
    {
        [DataMember(Order = 1)]
        public TraceVector? ExteriorVector { get; set; }

        [DataMember(Order = 2)]
        public TraceVector? EastBasis { get; set; }

        [DataMember(Order = 3)]
        public TraceVector? NorthBasis { get; set; }

        [DataMember(Order = 4)]
        public string? Bucket { get; set; }
    }

    public static class CalculationTraceWriter
    {
        public static byte[] Serialize(CalculationTrace trace)
        {
            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            var serializer = new DataContractJsonSerializer(typeof(CalculationTrace));
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, trace);
                return stream.ToArray();
            }
        }
    }
}
