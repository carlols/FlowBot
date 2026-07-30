namespace FlowBot;

public static partial class GroupFinderButtonIds
{
    public static bool TryParse(string customId, out GroupFinderButtonState state)
    {
        if (TryParse(customId, JoinPrefix, GroupFinderButtonAction.Join, out state))
        {
            return true;
        }

        if (TryParse(customId, LeavePrefix, GroupFinderButtonAction.Leave, out state))
        {
            return true;
        }

        if (TryParse(customId, ReadyCheckPrefix, GroupFinderButtonAction.ReadyCheck, out state))
        {
            return true;
        }

        if (TryParse(customId, EditTimePrefix, GroupFinderButtonAction.EditTime, out state))
        {
            return true;
        }

        if (TryParse(customId, StartPrefix, GroupFinderButtonAction.Start, out state))
        {
            return true;
        }

        if (TryParse(customId, ScrambleTeamsPrefix, GroupFinderButtonAction.ScrambleTeams, out state))
        {
            return true;
        }

        return TryParse(customId, ClosePrefix, GroupFinderButtonAction.Close, out state);
    }

    public static bool TryParseReadyCheckModal(string customId, out GroupFinderReadyCheckModalState state)
    {
        state = new GroupFinderReadyCheckModalState(0, null, null, null);

        if (!customId.StartsWith(ReadyCheckModalPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var values = customId[ReadyCheckModalPrefix.Length..].Split(':');

        if (values.Length != 4
            || !ulong.TryParse(values[0], out var messageId)
            || !TryParseCapacity(values[1], out var capacity)
            || !TryParseState(values[2], out var capacityNoticeSent)
            || !TryParseState(values[3], out var sessionStarted))
        {
            return false;
        }

        state = new GroupFinderReadyCheckModalState(messageId, capacity, capacityNoticeSent, sessionStarted);
        return true;
    }

    public static bool TryParseEditTimeModal(string customId, out GroupFinderEditTimeModalState state)
    {
        state = new GroupFinderEditTimeModalState(0, null, null, null);

        if (!customId.StartsWith(EditTimeModalPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var values = customId[EditTimeModalPrefix.Length..].Split(':');

        if (values.Length != 4
            || !ulong.TryParse(values[0], out var messageId)
            || !TryParseCapacity(values[1], out var capacity)
            || !TryParseState(values[2], out var capacityNoticeSent)
            || !TryParseState(values[3], out var sessionStarted))
        {
            return false;
        }

        state = new GroupFinderEditTimeModalState(messageId, capacity, capacityNoticeSent, sessionStarted);
        return true;
    }

    public static bool TryParseReadyResponse(string customId, out GroupFinderReadyResponseState state)
    {
        if (TryParseReadyResponse(customId, ReadyPrefix, GroupFinderButtonAction.Ready, out state))
        {
            return true;
        }

        return TryParseReadyResponse(customId, NotReadyPrefix, GroupFinderButtonAction.NotReady, out state);
    }

    public static bool TryParseStartConfirmation(string customId, out GroupFinderStartConfirmation confirmation)
    {
        confirmation = new GroupFinderStartConfirmation(GroupFinderButtonAction.ConfirmStart, 0, 0, null, null, null);

        if (customId == CancelStartPrefix)
        {
            confirmation = confirmation with { Action = GroupFinderButtonAction.CancelStart };
            return true;
        }

        if (!customId.StartsWith(ConfirmStartPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var values = customId[ConfirmStartPrefix.Length..].Split(':');

        if (values.Length is < 3 or > 5
            || !ulong.TryParse(values[0], out var messageId)
            || !ulong.TryParse(values[1], out var hostUserId)
            || !TryParseCapacity(values[2], out var capacity)
            || !TryParseOptionalStates(values, 3, out var capacityNoticeSent, out var sessionStarted))
        {
            return false;
        }
        confirmation = new GroupFinderStartConfirmation(
            GroupFinderButtonAction.ConfirmStart,
            messageId,
            hostUserId,
            capacity,
            capacityNoticeSent,
            sessionStarted);
        return true;
    }

    public static bool TryParseCloseConfirmation(string customId, out GroupFinderCloseConfirmation confirmation)
    {
        confirmation = new GroupFinderCloseConfirmation(GroupFinderButtonAction.ConfirmClose, 0, 0);

        if (customId == CancelClosePrefix)
        {
            confirmation = confirmation with { Action = GroupFinderButtonAction.CancelClose };
            return true;
        }

        if (!customId.StartsWith(ConfirmClosePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var values = customId[ConfirmClosePrefix.Length..].Split(':');

        if (values.Length != 2
            || !ulong.TryParse(values[0], out var messageId)
            || !ulong.TryParse(values[1], out var hostUserId))
        {
            return false;
        }

        confirmation = new GroupFinderCloseConfirmation(
            GroupFinderButtonAction.ConfirmClose,
            messageId,
            hostUserId);
        return true;
    }

    private static bool TryParse(
        string customId,
        string prefix,
        GroupFinderButtonAction expectedAction,
        out GroupFinderButtonState state)
    {
        state = new GroupFinderButtonState(expectedAction, null, null, null);

        if (!customId.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var values = customId[prefix.Length..].Split(':');

        if (values.Length is < 1 or > 3
            || !TryParseCapacity(values[0], out var capacity)
            || !TryParseOptionalStates(values, 1, out var capacityNoticeSent, out var sessionStarted))
        {
            return false;
        }
        state = new GroupFinderButtonState(expectedAction, capacity, capacityNoticeSent, sessionStarted);
        return true;
    }

    private static bool TryParseOptionalStates(
        string[] values,
        int startIndex,
        out bool? capacityNoticeSent,
        out bool? sessionStarted)
    {
        capacityNoticeSent = null;
        sessionStarted = null;

        if (values.Length == startIndex)
        {
            return true;
        }

        if (!TryParseState(values[startIndex], out var firstState))
        {
            return false;
        }

        capacityNoticeSent = firstState;

        if (values.Length == startIndex + 1)
        {
            sessionStarted = firstState;
            return true;
        }

        if (values.Length == startIndex + 2
            && TryParseState(values[startIndex + 1], out var secondState))
        {
            sessionStarted = secondState;
            return true;
        }

        return false;
    }

    private static bool TryParseState(string value, out bool state)
    {
        state = value == "1";
        return state || value == "0";
    }
    private static string FormatCapacity(int? capacity) =>
        capacity?.ToString() ?? OpenCapacityToken;

    private static bool TryParseCapacity(string value, out int? capacity)
    {
        capacity = null;

        if (string.Equals(value, OpenCapacityToken, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!int.TryParse(value, out var parsedCapacity)
            || parsedCapacity is < GroupFinderSession.MinCapacity or > GroupFinderSession.MaxCapacity)
        {
            return false;
        }

        capacity = parsedCapacity;
        return true;
    }

    private static string FormatState(bool value) =>
        value ? "1" : "0";

    private static bool TryParseReadyResponse(
        string customId,
        string prefix,
        GroupFinderButtonAction action,
        out GroupFinderReadyResponseState state)
    {
        state = new GroupFinderReadyResponseState(action, 0, null, null, null);

        if (!customId.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var values = customId[prefix.Length..].Split(':');

        if (values.Length != 4
            || !ulong.TryParse(values[0], out var messageId)
            || !TryParseCapacity(values[1], out var capacity)
            || !TryParseState(values[2], out var capacityNoticeSent)
            || !TryParseState(values[3], out var sessionStarted))
        {
            return false;
        }

        state = new GroupFinderReadyResponseState(action, messageId, capacity, capacityNoticeSent, sessionStarted);
        return true;
    }
}
