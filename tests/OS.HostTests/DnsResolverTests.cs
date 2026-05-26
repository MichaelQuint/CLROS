using System.Runtime.CompilerServices;
using OS.Net;

internal static class DnsResolverTests
{
    private const ushort TransactionId = 0x2345;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("net.dns resolver writes query and resolves response", TestWritesQueryAndResolvesResponse, ref failures);
        Run("net.dns resolver rejects mismatched transaction id", TestRejectsMismatchedTransactionId, ref failures);
        Run("net.dns resolver times out query", TestTimesOutQuery, ref failures);
        Run("net.dns resolver default result is not success", TestDefaultResultIsNotSuccess, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("DNS resolver host tests failed: " + failures);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TestWritesQueryAndResolvesResponse()
    {
        DnsResolverTransaction transaction = DnsResolverTransaction.Create(TransactionId);
        Span<byte> query = stackalloc byte[64];

        DnsResolverStatus queryStatus = transaction.TryWriteQuery(
            query,
            "example.com",
            out DnsResolverTransitionResult queryResult);
        if (queryStatus != DnsResolverStatus.Success
            || !queryResult.Succeeded
            || queryResult.State != DnsResolverState.QueryWritten
            || transaction.State != DnsResolverState.QueryWritten
            || queryResult.BytesWritten != ExpectedQueryLength
            || query[0] != 0x23
            || query[1] != 0x45
            || query[2] != 0x01
            || query[3] != 0x00
            || query[4] != 0x00
            || query[5] != 0x01
            || query[queryResult.BytesWritten - 4] != 0x00
            || query[queryResult.BytesWritten - 3] != 0x01
            || query[queryResult.BytesWritten - 2] != 0x00
            || query[queryResult.BytesWritten - 1] != 0x01)
        {
            return false;
        }

        byte[] response = CreateCompressedResponse(
            TransactionId,
            flags: 0x8180,
            answerType: DnsMessage.TypeA,
            answerClass: DnsMessage.ClassIn,
            answerDataLength: 4);
        DnsResolverStatus responseStatus = transaction.ReceiveResponse(
            response,
            out DnsResolverTransitionResult responseResult);

        return responseStatus == DnsResolverStatus.Success
            && responseResult.Succeeded
            && responseResult.State == DnsResolverState.Resolved
            && responseResult.Address == ResolvedAddress()
            && transaction.State == DnsResolverState.Resolved
            && transaction.ResolvedAddress == ResolvedAddress();
    }

    private static bool TestRejectsMismatchedTransactionId()
    {
        DnsResolverTransaction transaction = DnsResolverTransaction.Create(TransactionId);
        Span<byte> query = stackalloc byte[64];
        DnsResolverStatus queryStatus = transaction.TryWriteQuery(
            query,
            "example.com",
            out DnsResolverTransitionResult _);
        if (queryStatus != DnsResolverStatus.Success)
        {
            return false;
        }

        byte[] response = CreateCompressedResponse(
            TransactionId + 1,
            flags: 0x8180,
            answerType: DnsMessage.TypeA,
            answerClass: DnsMessage.ClassIn,
            answerDataLength: 4);
        DnsResolverStatus responseStatus = transaction.ReceiveResponse(
            response,
            out DnsResolverTransitionResult responseResult);

        return responseStatus == DnsResolverStatus.TransactionIdMismatch
            && !responseResult.Succeeded
            && responseResult.State == DnsResolverState.Failed
            && responseResult.MessageStatus == DnsMessageStatus.TransactionIdMismatch
            && transaction.State == DnsResolverState.Failed
            && RejectsQuestionMismatch()
            && RejectsAnswerNameMismatch()
            && RejectsUnsupportedResponse()
            && RejectsMalformedResponse();
    }

    private static bool TestTimesOutQuery()
    {
        DnsResolverTransaction transaction = DnsResolverTransaction.Create(TransactionId);
        Span<byte> query = stackalloc byte[64];
        DnsResolverStatus queryStatus = transaction.TryWriteQuery(
            query,
            "example.com",
            out DnsResolverTransitionResult _);
        if (queryStatus != DnsResolverStatus.Success)
        {
            return false;
        }

        DnsResolverStatus timeoutStatus = transaction.Timeout(out DnsResolverTransitionResult timeoutResult);

        return timeoutStatus == DnsResolverStatus.Timeout
            && !timeoutResult.Succeeded
            && timeoutResult.State == DnsResolverState.Failed
            && transaction.State == DnsResolverState.Failed;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        DnsResolverTransitionResult defaultResult = default;
        DnsResolverTransitionResult failedResult =
            DnsResolverTransitionResult.Fail(
                DnsResolverStatus.InvalidState,
                DnsResolverState.Initial,
                DnsMessageStatus.Success);

        return !defaultResult.IsInitialized
            && !defaultResult.Succeeded
            && defaultResult.Status == DnsResolverStatus.Success
            && failedResult.IsInitialized
            && !failedResult.Succeeded
            && failedResult.Status == DnsResolverStatus.InvalidState
            && failedResult.State == DnsResolverState.Initial
            && failedResult.MessageStatus == DnsMessageStatus.Success
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                DnsResolverTransitionResult>();
    }

    private static bool RejectsUnsupportedResponse()
    {
        DnsResolverTransaction transaction = DnsResolverTransaction.Create(TransactionId);
        Span<byte> query = stackalloc byte[64];
        if (transaction.TryWriteQuery(query, "example.com", out DnsResolverTransitionResult _) != DnsResolverStatus.Success)
        {
            return false;
        }

        byte[] response = CreateCompressedResponse(
            TransactionId,
            flags: 0x8180,
            answerType: 28,
            answerClass: DnsMessage.ClassIn,
            answerDataLength: 4);
        DnsResolverStatus status = transaction.ReceiveResponse(
            response,
            out DnsResolverTransitionResult result);

        return status == DnsResolverStatus.MessageParseFailed
            && !result.Succeeded
            && result.State == DnsResolverState.Failed
            && result.MessageStatus == DnsMessageStatus.UnsupportedType
            && transaction.State == DnsResolverState.Failed;
    }

    private static bool RejectsQuestionMismatch()
    {
        DnsResolverTransaction transaction = DnsResolverTransaction.Create(TransactionId);
        Span<byte> query = stackalloc byte[64];
        if (transaction.TryWriteQuery(query, "example.com", out DnsResolverTransitionResult _) != DnsResolverStatus.Success)
        {
            return false;
        }

        byte[] response = CreateCompressedResponse(
            TransactionId,
            flags: 0x8180,
            answerType: DnsMessage.TypeA,
            answerClass: DnsMessage.ClassIn,
            answerDataLength: 4);
        response[DnsMessage.HeaderLength + 1] = 0x78;
        DnsResolverStatus status = transaction.ReceiveResponse(
            response,
            out DnsResolverTransitionResult result);

        return status == DnsResolverStatus.QuestionMismatch
            && !result.Succeeded
            && result.State == DnsResolverState.Failed
            && transaction.State == DnsResolverState.Failed;
    }

    private static bool RejectsAnswerNameMismatch()
    {
        DnsResolverTransaction transaction = DnsResolverTransaction.Create(TransactionId);
        Span<byte> query = stackalloc byte[64];
        if (transaction.TryWriteQuery(query, "example.com", out DnsResolverTransitionResult _) != DnsResolverStatus.Success)
        {
            return false;
        }

        byte[] response = CreateCompressedResponse(
            TransactionId,
            flags: 0x8180,
            answerType: DnsMessage.TypeA,
            answerClass: DnsMessage.ClassIn,
            answerDataLength: 4);
        int answerNameOffset = DnsMessage.HeaderLength + EncodedExampleComLength + 4;
        response[answerNameOffset] = 0xC0;
        response[answerNameOffset + 1] = DnsMessage.HeaderLength + 8;
        DnsResolverStatus status = transaction.ReceiveResponse(
            response,
            out DnsResolverTransitionResult result);

        return status == DnsResolverStatus.AnswerNameMismatch
            && !result.Succeeded
            && result.State == DnsResolverState.Failed
            && transaction.State == DnsResolverState.Failed;
    }

    private static bool RejectsMalformedResponse()
    {
        DnsResolverTransaction transaction = DnsResolverTransaction.Create(TransactionId);
        Span<byte> query = stackalloc byte[64];
        if (transaction.TryWriteQuery(query, "example.com", out DnsResolverTransitionResult _) != DnsResolverStatus.Success)
        {
            return false;
        }

        byte[] response = CreateCompressedResponse(
            TransactionId,
            flags: 0x8180,
            answerType: DnsMessage.TypeA,
            answerClass: DnsMessage.ClassIn,
            answerDataLength: 4);
        DnsResolverStatus status = transaction.ReceiveResponse(
            response.AsSpan(0, response.Length - 1),
            out DnsResolverTransitionResult result);

        return status == DnsResolverStatus.MessageParseFailed
            && !result.Succeeded
            && result.State == DnsResolverState.Failed
            && result.MessageStatus == DnsMessageStatus.TruncatedAnswer
            && transaction.State == DnsResolverState.Failed;
    }

    private const int EncodedExampleComLength = 13;
    private const int ExpectedQueryLength = DnsMessage.HeaderLength + EncodedExampleComLength + 4;

    private static byte[] CreateCompressedResponse(
        int transactionId,
        ushort flags,
        ushort answerType,
        ushort answerClass,
        ushort answerDataLength)
    {
        byte[] response = new byte[DnsMessage.HeaderLength + EncodedExampleComLength + 4 + 2 + 10 + answerDataLength];

        response[0] = (byte)((ushort)transactionId >> 8);
        response[1] = (byte)transactionId;
        response[2] = (byte)(flags >> 8);
        response[3] = (byte)flags;
        response[4] = 0x00;
        response[5] = 0x01;
        response[6] = 0x00;
        response[7] = 0x01;
        response[8] = 0x00;
        response[9] = 0x00;
        response[10] = 0x00;
        response[11] = 0x00;

        int offset = DnsMessage.HeaderLength;
        response[offset++] = 0x07;
        response[offset++] = 0x65;
        response[offset++] = 0x78;
        response[offset++] = 0x61;
        response[offset++] = 0x6D;
        response[offset++] = 0x70;
        response[offset++] = 0x6C;
        response[offset++] = 0x65;
        response[offset++] = 0x03;
        response[offset++] = 0x63;
        response[offset++] = 0x6F;
        response[offset++] = 0x6D;
        response[offset++] = 0x00;
        response[offset++] = 0x00;
        response[offset++] = 0x01;
        response[offset++] = 0x00;
        response[offset++] = 0x01;

        response[offset++] = 0xC0;
        response[offset++] = 0x0C;
        response[offset++] = (byte)(answerType >> 8);
        response[offset++] = (byte)answerType;
        response[offset++] = (byte)(answerClass >> 8);
        response[offset++] = (byte)answerClass;
        response[offset++] = 0x00;
        response[offset++] = 0x00;
        response[offset++] = 0x00;
        response[offset++] = 0x3C;
        response[offset++] = (byte)(answerDataLength >> 8);
        response[offset++] = (byte)answerDataLength;

        if (answerDataLength > 0)
        {
            response[offset++] = 0x5D;
        }

        if (answerDataLength > 1)
        {
            response[offset++] = 0xB8;
        }

        if (answerDataLength > 2)
        {
            response[offset++] = 0xD8;
        }

        if (answerDataLength > 3)
        {
            response[offset] = 0x22;
        }

        return response;
    }

    private static Ipv4Address ResolvedAddress()
    {
        return new Ipv4Address(93, 184, 216, 34);
    }
}
