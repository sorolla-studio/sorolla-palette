using System;

namespace Sorolla.Palette
{
    internal enum SorollaDiagnosticSeverity
    {
        Info,
        Waiting,
        Pass,
        Warning,
        Fail,
    }

    internal enum SorollaDiagnosticKind
    {
        Required,
        Observed,
        Context,
    }

    internal readonly struct SorollaDiagnosticRow
    {
        public readonly string Group;
        public readonly string Name;
        public readonly SorollaDiagnosticSeverity Severity;
        public readonly string Detail;
        public readonly SorollaDiagnosticKind Kind;

        // Phase 5 (message content pass, spec sections 3/8/10): optional structured three-part
        // diagnosis. Additive - null on every row that predates this pass or whose row-producing
        // site genuinely cannot know more than the free-text Detail (see SorollaDiagnostics.
        // Diagnoses.cs for which classes are wired vs still fallback, and why). Public API
        // (Palette.*) is untouched; this struct is internal.
        public readonly string Why;
        public readonly string Signal;
        public readonly string Fix;

        public SorollaDiagnosticRow(string group, string name, SorollaDiagnosticSeverity severity, string detail,
            SorollaDiagnosticKind kind, string why = null, string signal = null, string fix = null)
        {
            Group = group;
            Name = name;
            Severity = severity;
            Detail = detail;
            Kind = kind;
            Why = why;
            Signal = signal;
            Fix = fix;
        }

        public bool HasStructuredDiagnosis => Why != null && Signal != null && Fix != null;
    }

    internal readonly struct SorollaRuntimeProblem
    {
        public readonly int Id;
        public readonly string Fingerprint;
        public readonly float FirstTimeSeconds;
        public readonly float LastTimeSeconds;
        public readonly int Count;
        public readonly SorollaDiagnosticSeverity Severity;
        public readonly string Source;
        public readonly string Type;
        public readonly string Message;
        public readonly string TopFrame;
        public readonly string StackTrace;

        public SorollaRuntimeProblem(int id, string fingerprint, float firstTimeSeconds, float lastTimeSeconds,
            int count, SorollaDiagnosticSeverity severity, string source, string type, string message,
            string topFrame, string stackTrace)
        {
            Id = id;
            Fingerprint = fingerprint;
            FirstTimeSeconds = firstTimeSeconds;
            LastTimeSeconds = lastTimeSeconds;
            Count = count;
            Severity = severity;
            Source = source;
            Type = type;
            Message = message;
            TopFrame = topFrame;
            StackTrace = stackTrace;
        }

        public SorollaRuntimeProblem WithRepeat(float timeSeconds, SorollaDiagnosticSeverity severity)
        {
            return new SorollaRuntimeProblem(Id, Fingerprint, FirstTimeSeconds, timeSeconds, Count + 1,
                severity, Source, Type, Message, TopFrame, StackTrace);
        }
    }

    internal readonly struct SorollaDiagnosticEventLogEntry
    {
        public readonly int Id;
        public readonly float TimeSeconds;
        public readonly string Source;
        public readonly string Name;
        public readonly string Payload;
        public readonly SorollaDiagnosticPayloadLine[] PayloadLines;

        public SorollaDiagnosticEventLogEntry(int id, float timeSeconds, string source, string name, string payload,
            SorollaDiagnosticPayloadLine[] payloadLines)
        {
            Id = id;
            TimeSeconds = timeSeconds;
            Source = source;
            Name = name;
            Payload = payload;
            PayloadLines = payloadLines ?? Array.Empty<SorollaDiagnosticPayloadLine>();
        }
    }

    internal readonly struct SorollaDiagnosticPayloadLine
    {
        public readonly string Key;
        public readonly string Value;

        public SorollaDiagnosticPayloadLine(string key, string value)
        {
            Key = key;
            Value = value;
        }
    }
}
