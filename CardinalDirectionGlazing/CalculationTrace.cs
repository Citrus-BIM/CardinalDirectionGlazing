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
            SourceCollectionCounts = new List<SourceCollectionTrace>();
            CollectionDiagnostics = new List<CollectionDiagnosticTrace>();
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

        [DataMember(Order = 5)]
        public DocumentTrace? HostDocument { get; set; }

        [DataMember(Order = 6)]
        public LinkTrace? SelectedLink { get; set; }

        [DataMember(Order = 7)]
        public DirectionTrace? TrueNorth { get; set; }

        [DataMember(Order = 8)]
        public int TargetCount { get; set; }

        [DataMember(Order = 9)]
        public List<SourceCollectionTrace> SourceCollectionCounts { get; set; }

        [DataMember(Order = 10)]
        public List<CollectionDiagnosticTrace> CollectionDiagnostics { get; set; }

        [DataMember(Order = 11)]
        public string? Outcome { get; set; }

        [DataMember(Order = 12)]
        public string? ReasonCode { get; set; }

        [DataMember(Order = 13)]
        public string? Error { get; set; }

        public TargetTrace StartTarget(string uniqueId)
        {
            var target = new TargetTrace(uniqueId);
            Targets.Add(target);
            return target;
        }
    }

    [DataContract]
    public sealed class CollectionDiagnosticTrace
    {
        [DataMember(Order = 1)]
        public string? SourcePass { get; set; }

        [DataMember(Order = 2)]
        public string? SourceType { get; set; }

        [DataMember(Order = 3)]
        public string? ElementId { get; set; }

        [DataMember(Order = 4)]
        public string? UniqueId { get; set; }

        [DataMember(Order = 5)]
        public DocumentTrace? Document { get; set; }

        [DataMember(Order = 6)]
        public bool HasCurtainGrid { get; set; }

        [DataMember(Order = 7)]
        public string? ModelGroup { get; set; }

        [DataMember(Order = 8)]
        public string? ReasonCode { get; set; }

        [DataMember(Order = 9)]
        public string? Outcome { get; set; }

        [DataMember(Order = 10)]
        public string? SuperComponentElementId { get; set; }

        [DataMember(Order = 11)]
        public string? SuperComponentUniqueId { get; set; }

        [DataMember(Order = 12)]
        public string? Error { get; set; }
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

        [DataMember(Order = 8)]
        public string? ElementType { get; set; }

        [DataMember(Order = 9)]
        public bool SolidFound { get; set; }

        [DataMember(Order = 10)]
        public double? SolidVolume { get; set; }

        [DataMember(Order = 11)]
        public DirectionalAreasTrace? Totals { get; set; }

        [DataMember(Order = 12)]
        public List<ParameterWriteTrace>? ParameterWrites { get; set; }

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

        [DataMember(Order = 8)]
        public string? SourcePass { get; set; }

        [DataMember(Order = 9)]
        public DocumentTrace? Document { get; set; }

        [DataMember(Order = 10)]
        public string? SuperComponent { get; set; }

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
            Details = new Dictionary<string, string>();
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

        [DataMember(Order = 5)]
        public Dictionary<string, string> Details { get; set; }

        public void Complete(string outcome, string reasonCode)
        {
            Outcome = outcome;
            ReasonCode = reasonCode;
        }
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
    public sealed class DocumentTrace
    {
        [DataMember(Order = 1)]
        public string? Title { get; set; }

        [DataMember(Order = 2)]
        public string? PathName { get; set; }
    }

    [DataContract]
    public sealed class LinkTrace
    {
        [DataMember(Order = 1)]
        public string? ElementId { get; set; }

        [DataMember(Order = 2)]
        public string? UniqueId { get; set; }

        [DataMember(Order = 3)]
        public DocumentTrace? Document { get; set; }

        [DataMember(Order = 4)]
        public TransformTrace? Transform { get; set; }
    }

    [DataContract]
    public sealed class TransformTrace
    {
        [DataMember(Order = 1)]
        public TraceVector? Origin { get; set; }

        [DataMember(Order = 2)]
        public TraceVector? BasisX { get; set; }

        [DataMember(Order = 3)]
        public TraceVector? BasisY { get; set; }

        [DataMember(Order = 4)]
        public TraceVector? BasisZ { get; set; }
    }

    [DataContract]
    public sealed class SourceCollectionTrace
    {
        [DataMember(Order = 1)]
        public string? Source { get; set; }

        [DataMember(Order = 2)]
        public int Count { get; set; }
    }

    [DataContract]
    public sealed class DirectionalAreasTrace
    {
        [DataMember(Order = 1)] public double North { get; set; }
        [DataMember(Order = 2)] public double South { get; set; }
        [DataMember(Order = 3)] public double West { get; set; }
        [DataMember(Order = 4)] public double East { get; set; }
        [DataMember(Order = 5)] public double Northwest { get; set; }
        [DataMember(Order = 6)] public double Northeast { get; set; }
        [DataMember(Order = 7)] public double Southwest { get; set; }
        [DataMember(Order = 8)] public double Southeast { get; set; }
    }

    [DataContract]
    public sealed class ParameterWriteTrace
    {
        [DataMember(Order = 1)] public string? Guid { get; set; }
        [DataMember(Order = 2)] public double? OldValue { get; set; }
        [DataMember(Order = 3)] public double NewValue { get; set; }
        [DataMember(Order = 4)] public bool Exists { get; set; }
        [DataMember(Order = 5)] public bool IsReadOnly { get; set; }
        [DataMember(Order = 6)] public bool? SetSucceeded { get; set; }
        [DataMember(Order = 7)] public string? Error { get; set; }
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

        [DataMember(Order = 5)]
        public bool Accepted { get; set; }

        [DataMember(Order = 6)]
        public double Area { get; set; }

        [DataMember(Order = 7)]
        public double? BucketValueBefore { get; set; }

        [DataMember(Order = 8)]
        public double? BucketValueAfter { get; set; }
    }

    public static class CalculationTraceWriter
    {
        public static string CreateDesktopPath(DateTime localNow)
        {
            string fileName = string.Format(
                "CardinalDirectionGlazing_{0:yyyy-MM-dd_HHmmss}.json",
                localNow);
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                fileName);
        }

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

        public static bool TryWrite(CalculationTrace trace, out string path, out string error)
        {
            return TryWrite(trace, CreateDesktopPath(DateTime.Now), out path, out error);
        }

        public static bool TryWrite(CalculationTrace trace, string requestedPath, out string path, out string error)
        {
            path = string.Empty;
            error = string.Empty;

            try
            {
                string? directory = Path.GetDirectoryName(requestedPath);
                if (string.IsNullOrWhiteSpace(directory))
                {
                    directory = Environment.CurrentDirectory;
                }

                Directory.CreateDirectory(directory);
                string baseName = Path.GetFileNameWithoutExtension(requestedPath);
                string extension = Path.GetExtension(requestedPath);
                byte[] bytes = Serialize(trace);
                IOException? lastCollision = null;

                for (int suffix = 0; suffix < 1000; suffix++)
                {
                    string candidate = suffix == 0
                        ? Path.Combine(directory, baseName + extension)
                        : Path.Combine(directory, baseName + "_" + suffix + extension);

                    try
                    {
                        using (var stream = new FileStream(candidate, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        {
                            stream.Write(bytes, 0, bytes.Length);
                        }

                        path = candidate;
                        return true;
                    }
                    catch (IOException ex)
                    {
                        lastCollision = ex;
                    }
                }

                error = lastCollision?.Message ?? "Unable to create a unique trace file.";
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
