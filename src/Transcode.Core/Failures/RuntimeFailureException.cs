namespace Transcode.Core.Failures;

/*
Это typed runtime-failure для доменных и probe-ошибок.
CLI и formatter'ы могут классифицировать такие ошибки по коду, а не по тексту сообщения.
*/
/// <summary>
/// Represents a structured runtime failure that can be classified without parsing the exception message.
/// </summary>
public sealed class RuntimeFailureException : InvalidOperationException
{
    public RuntimeFailureException(RuntimeFailureCode code, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
    }

    public RuntimeFailureCode Code { get; }
}

/*
Короткие фабрики для типовых runtime-failure.
Нужны, чтобы код не размазывал по вызовам пары (code, message).
*/
/// <summary>
/// Creates common structured runtime failures used by probe, inspection, and scenario flows.
/// </summary>
public static class RuntimeFailures
{
    public static RuntimeFailureException ProbeProcessFailed(string message, Exception? innerException = null)
    {
        return new RuntimeFailureException(RuntimeFailureCode.ProbeProcessFailed, message, innerException);
    }

    public static RuntimeFailureException ProbeEmptyOutput()
    {
        return new RuntimeFailureException(RuntimeFailureCode.ProbeEmptyOutput, "ffprobe returned empty JSON output.");
    }

    public static RuntimeFailureException ProbeInvalidJson(Exception innerException)
    {
        return new RuntimeFailureException(RuntimeFailureCode.ProbeInvalidJson, "ffprobe returned invalid JSON output.", innerException);
    }

    public static RuntimeFailureException ProbeMissingStreamsField()
    {
        return new RuntimeFailureException(RuntimeFailureCode.ProbeMissingStreamsField, "ffprobe JSON is missing required field 'streams'.");
    }

    public static RuntimeFailureException ProbeInvalidStreamEntry()
    {
        return new RuntimeFailureException(RuntimeFailureCode.ProbeInvalidStreamEntry, "ffprobe JSON contained an invalid stream entry.");
    }

    public static RuntimeFailureException ProbeMissingRequiredField(string propertyName, string scope)
    {
        return new RuntimeFailureException(
            RuntimeFailureCode.ProbeMissingRequiredField,
            $"ffprobe JSON is missing required field '{propertyName}' in {scope}.");
    }

    public static RuntimeFailureException ProbeNoData()
    {
        return new RuntimeFailureException(RuntimeFailureCode.ProbeNoData, "Video probe returned no data.");
    }

    public static RuntimeFailureException ProbeNoStreams()
    {
        return new RuntimeFailureException(RuntimeFailureCode.ProbeNoStreams, "Video probe did not return any streams.");
    }

    public static RuntimeFailureException NoVideoStream()
    {
        return new RuntimeFailureException(RuntimeFailureCode.NoVideoStream, "Video probe did not return a video stream.");
    }

    public static RuntimeFailureException InvalidVideoWidth()
    {
        return new RuntimeFailureException(RuntimeFailureCode.InvalidVideoWidth, "Video probe did not return a valid video width.");
    }

    public static RuntimeFailureException InvalidVideoHeight()
    {
        return new RuntimeFailureException(RuntimeFailureCode.InvalidVideoHeight, "Video probe did not return a valid video height.");
    }

    public static RuntimeFailureException InvalidFrameRate()
    {
        return new RuntimeFailureException(RuntimeFailureCode.InvalidFrameRate, "Video probe did not return a valid frame rate.");
    }

    public static RuntimeFailureException DownscaleSourceBucketIssue(string message)
    {
        return new RuntimeFailureException(RuntimeFailureCode.DownscaleSourceBucketIssue, message);
    }
}

/*
Это перечисление структурированных кодов ошибок runtime.
По нему CLI и formatter-ы могут различать probe-проблемы и сценарные нарушения без парсинга текста сообщения.
*/
/// <summary>
/// Enumerates structured runtime failure kinds exposed by probe and scenario layers.
/// </summary>
public enum RuntimeFailureCode
{
    ProbeProcessFailed,
    ProbeEmptyOutput,
    ProbeInvalidJson,
    ProbeMissingStreamsField,
    ProbeInvalidStreamEntry,
    ProbeMissingRequiredField,
    ProbeNoData,
    ProbeNoStreams,
    NoVideoStream,
    InvalidVideoWidth,
    InvalidVideoHeight,
    InvalidFrameRate,
    DownscaleSourceBucketIssue
}

/*
Это helper-классификатор для кодов ошибок runtime.
Он инкапсулирует группировку кодов по смысловым категориям и не размазывает такие проверки по вызывающему коду.
*/
/// <summary>
/// Provides helper classification rules for structured runtime failures.
/// </summary>
public static class RuntimeFailureCodeExtensions
{
    public static bool IsProbeFailure(this RuntimeFailureCode code)
    {
        return code is RuntimeFailureCode.ProbeProcessFailed
            or RuntimeFailureCode.ProbeEmptyOutput
            or RuntimeFailureCode.ProbeInvalidJson
            or RuntimeFailureCode.ProbeMissingStreamsField
            or RuntimeFailureCode.ProbeInvalidStreamEntry
            or RuntimeFailureCode.ProbeMissingRequiredField
            or RuntimeFailureCode.ProbeNoData
            or RuntimeFailureCode.ProbeNoStreams
            or RuntimeFailureCode.InvalidVideoWidth
            or RuntimeFailureCode.InvalidVideoHeight
            or RuntimeFailureCode.InvalidFrameRate;
    }

    public static bool IsUnknownDimensions(this RuntimeFailureCode code)
    {
        return code is RuntimeFailureCode.InvalidVideoWidth or RuntimeFailureCode.InvalidVideoHeight;
    }
}
