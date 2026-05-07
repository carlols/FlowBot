namespace FlowBot;

public static class PullCountGuessIds
{
    public const string PullCountInputId = "flowbot-pulls-input";

    private const string AddOrUpdatePrefix = "flowbot-pulls-add:";
    private const string RemovePrefix = "flowbot-pulls-remove:";
    private const string ClosePrefix = "flowbot-pulls-close:";
    private const string ConfirmClosePrefix = "flowbot-pulls-confirm-close:";
    private const string CancelClosePrefix = "flowbot-pulls-cancel-close:";
    private const string ModalPrefix = "flowbot-pulls-modal:";

    public static string CreateAddOrUpdateId(bool isClosed) =>
        $"{AddOrUpdatePrefix}{FormatState(isClosed)}";

    public static string CreateRemoveId(bool isClosed) =>
        $"{RemovePrefix}{FormatState(isClosed)}";

    public static string CreateCloseId(bool isClosed) =>
        $"{ClosePrefix}{FormatState(isClosed)}";

    public static string CreateConfirmCloseId(ulong messageId) =>
        $"{ConfirmClosePrefix}{messageId}";

    public static string CreateCancelCloseId() => CancelClosePrefix;

    public static string CreateModalId(ulong messageId) =>
        $"{ModalPrefix}{messageId}";

    public static bool IsPullCountGuessInteraction(string customId) =>
        customId.StartsWith(AddOrUpdatePrefix, StringComparison.Ordinal)
        || customId.StartsWith(RemovePrefix, StringComparison.Ordinal)
        || customId.StartsWith(ClosePrefix, StringComparison.Ordinal)
        || customId.StartsWith(ConfirmClosePrefix, StringComparison.Ordinal)
        || customId.StartsWith(CancelClosePrefix, StringComparison.Ordinal)
        || customId.StartsWith(ModalPrefix, StringComparison.Ordinal);

    public static bool TryParseButton(string customId, out PullCountGuessButtonState state)
    {
        if (TryParseButton(customId, AddOrUpdatePrefix, PullCountGuessButtonAction.AddOrUpdate, out state))
        {
            return true;
        }

        if (TryParseButton(customId, RemovePrefix, PullCountGuessButtonAction.Remove, out state))
        {
            return true;
        }

        return TryParseButton(customId, ClosePrefix, PullCountGuessButtonAction.Close, out state);
    }

    public static bool TryParseCloseConfirmation(
        string customId,
        out PullCountGuessCloseConfirmation confirmation)
    {
        confirmation = new PullCountGuessCloseConfirmation(PullCountGuessButtonAction.ConfirmClose, 0);

        if (customId == CancelClosePrefix)
        {
            confirmation = confirmation with { Action = PullCountGuessButtonAction.CancelClose };
            return true;
        }

        if (!customId.StartsWith(ConfirmClosePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        if (!ulong.TryParse(customId[ConfirmClosePrefix.Length..], out var messageId))
        {
            return false;
        }

        confirmation = confirmation with { MessageId = messageId };
        return true;
    }

    public static bool TryParseModal(string customId, out PullCountGuessModalState state)
    {
        state = new PullCountGuessModalState(0);

        if (!customId.StartsWith(ModalPrefix, StringComparison.Ordinal)
            || !ulong.TryParse(customId[ModalPrefix.Length..], out var messageId))
        {
            return false;
        }

        state = new PullCountGuessModalState(messageId);
        return true;
    }

    private static bool TryParseButton(
        string customId,
        string prefix,
        PullCountGuessButtonAction action,
        out PullCountGuessButtonState state)
    {
        state = new PullCountGuessButtonState(action, false);

        if (!customId.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        if (!TryParseState(customId[prefix.Length..], out var isClosed))
        {
            return false;
        }

        state = new PullCountGuessButtonState(action, isClosed);
        return true;
    }

    private static bool TryParseState(string value, out bool state)
    {
        state = value == "1";

        return value is "0" or "1";
    }

    private static string FormatState(bool value) =>
        value ? "1" : "0";
}
