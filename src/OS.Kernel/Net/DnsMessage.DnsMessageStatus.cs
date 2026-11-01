namespace OS.Net;
using OS.Std.Binary;

public enum DnsMessageStatus
{
    Success = 0,
    BufferTooSmall,
    InvalidLength,
    InvalidLabel,
    TransactionIdMismatch,
    NotResponse,
    UnsupportedOpcode,
    TruncatedResponse,
    ResponseCodeFailure,
    UnsupportedQuestionCount,
    NoAnswer,
    UnsupportedType,
    UnsupportedClass,
    TruncatedQuestion,
    TruncatedAnswer,
    CompressionPointerLoop,
    CompressionPointerOutOfRange
}
