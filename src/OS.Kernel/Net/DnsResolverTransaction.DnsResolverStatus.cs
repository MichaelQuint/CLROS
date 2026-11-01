namespace OS.Net;
public enum DnsResolverStatus
{
    Success = 0,
    InvalidState = 1,
    BufferTooSmall = 2,
    MessageWriteFailed = 3,
    MessageParseFailed = 4,
    TransactionIdMismatch = 5,
    Timeout = 6,
    QuestionMismatch = 7,
    AnswerNameMismatch = 8
}
