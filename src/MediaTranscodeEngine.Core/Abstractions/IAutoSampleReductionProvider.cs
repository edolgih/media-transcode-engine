namespace MediaTranscodeEngine.Core.Abstractions;

public sealed record AutoSampleReductionInput(
    string InputPath,
    int Cq,
    double Maxrate,
    double Bufsize);

public interface IAutoSampleReductionProvider
{
    double? EstimateAccurate(AutoSampleReductionInput input);

    double? EstimateFast(AutoSampleReductionInput input);
}
