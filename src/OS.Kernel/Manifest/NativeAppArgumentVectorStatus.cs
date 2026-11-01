namespace OS.Manifest;
public enum NativeAppArgumentVectorStatus
{
    Success = 0,
    LaunchPlanFailed,
    InvalidArgumentsField,
    TooManyArguments,
    ArgumentTooLong,
    UnterminatedQuote,
    QuoteNotDelimited
}
