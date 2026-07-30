namespace FlowBot;

public static partial class GroupFinderButtonIds
{
    private const string JoinPrefix = "flowbot-group-join:";
    private const string LeavePrefix = "flowbot-group-leave:";
    private const string ReadyCheckPrefix = "flowbot-group-ready-check:";
    private const string ReadyCheckModalPrefix = "flowbot-group-ready-modal:";
    private const string EditTimePrefix = "flowbot-group-edit-time:";
    private const string EditTimeModalPrefix = "flowbot-group-edit-time-modal:";
    private const string ScrambleTeamsPrefix = "flowbot-group-scramble-teams:";
    private const string MovePlayersPrefix = "flowbot-group-move-players:";
    private const string VoiceChannelSelectPrefix = "flowbot-group-voice-select:";
    private const string ReadyPrefix = "flowbot-group-ready:";
    private const string NotReadyPrefix = "flowbot-group-not-ready:";
    private const string StartPrefix = "flowbot-group-start:";
    private const string ConfirmStartPrefix = "flowbot-group-confirm-start:";
    private const string CancelStartPrefix = "flowbot-group-cancel-start:";
    private const string ClosePrefix = "flowbot-group-close:";
    private const string ConfirmClosePrefix = "flowbot-group-confirm-close:";
    private const string CancelClosePrefix = "flowbot-group-cancel-close:";
    private const string OpenCapacityToken = "open";

    public static string CreateJoinId(int? capacity, bool capacityNoticeSent, bool sessionStarted) =>
        $"{JoinPrefix}{FormatCapacity(capacity)}:{FormatState(capacityNoticeSent)}:{FormatState(sessionStarted)}";

    public static string CreateLeaveId(int? capacity, bool capacityNoticeSent, bool sessionStarted) =>
        $"{LeavePrefix}{FormatCapacity(capacity)}:{FormatState(capacityNoticeSent)}:{FormatState(sessionStarted)}";

    public static string CreateReadyCheckId(int? capacity, bool capacityNoticeSent, bool sessionStarted) =>
        $"{ReadyCheckPrefix}{FormatCapacity(capacity)}:{FormatState(capacityNoticeSent)}:{FormatState(sessionStarted)}";

    public static string CreateReadyCheckModalId(
        ulong messageId,
        int? capacity,
        bool capacityNoticeSent,
        bool sessionStarted) =>
        $"{ReadyCheckModalPrefix}{messageId}:{FormatCapacity(capacity)}:{FormatState(capacityNoticeSent)}:{FormatState(sessionStarted)}";

    public static string CreateEditTimeId(int? capacity, bool capacityNoticeSent, bool sessionStarted) =>
        $"{EditTimePrefix}{FormatCapacity(capacity)}:{FormatState(capacityNoticeSent)}:{FormatState(sessionStarted)}";

    public static string CreateScrambleTeamsId(int? capacity, bool capacityNoticeSent, bool sessionStarted) =>
        $"{ScrambleTeamsPrefix}{FormatCapacity(capacity)}:{FormatState(capacityNoticeSent)}:{FormatState(sessionStarted)}";

    public static string CreateMovePlayersId(int? capacity, bool capacityNoticeSent, bool sessionStarted) =>
        $"{MovePlayersPrefix}{FormatCapacity(capacity)}:{FormatState(capacityNoticeSent)}:{FormatState(sessionStarted)}";

    public static string CreateVoiceChannelSelectId(ulong messageId) =>
        $"{VoiceChannelSelectPrefix}{messageId}";

    public static string CreateEditTimeModalId(
        ulong messageId,
        int? capacity,
        bool capacityNoticeSent,
        bool sessionStarted) =>
        $"{EditTimeModalPrefix}{messageId}:{FormatCapacity(capacity)}:{FormatState(capacityNoticeSent)}:{FormatState(sessionStarted)}";

    public static string CreateReadyId(
        ulong messageId,
        int? capacity,
        bool capacityNoticeSent,
        bool sessionStarted) =>
        $"{ReadyPrefix}{messageId}:{FormatCapacity(capacity)}:{FormatState(capacityNoticeSent)}:{FormatState(sessionStarted)}";

    public static string CreateNotReadyId(
        ulong messageId,
        int? capacity,
        bool capacityNoticeSent,
        bool sessionStarted) =>
        $"{NotReadyPrefix}{messageId}:{FormatCapacity(capacity)}:{FormatState(capacityNoticeSent)}:{FormatState(sessionStarted)}";

    public static string CreateStartId(int? capacity, bool capacityNoticeSent, bool sessionStarted) =>
        $"{StartPrefix}{FormatCapacity(capacity)}:{FormatState(capacityNoticeSent)}:{FormatState(sessionStarted)}";

    public static string CreateConfirmStartId(
        ulong messageId,
        ulong hostUserId,
        int? capacity,
        bool capacityNoticeSent,
        bool sessionStarted) =>
        $"{ConfirmStartPrefix}{messageId}:{hostUserId}:{FormatCapacity(capacity)}:{FormatState(capacityNoticeSent)}:{FormatState(sessionStarted)}";

    public static string CreateCancelStartId() => CancelStartPrefix;

    public static string CreateCloseId(int? capacity, bool capacityNoticeSent, bool sessionStarted) =>
        $"{ClosePrefix}{FormatCapacity(capacity)}:{FormatState(capacityNoticeSent)}:{FormatState(sessionStarted)}";

    public static string CreateConfirmCloseId(ulong messageId, ulong hostUserId) =>
        $"{ConfirmClosePrefix}{messageId}:{hostUserId}";

    public static string CreateCancelCloseId() => CancelClosePrefix;

    public static bool IsGroupFinderButton(string customId) =>
        customId.StartsWith(JoinPrefix, StringComparison.Ordinal)
        || customId.StartsWith(LeavePrefix, StringComparison.Ordinal)
        || customId.StartsWith(ReadyCheckPrefix, StringComparison.Ordinal)
        || customId.StartsWith(ReadyCheckModalPrefix, StringComparison.Ordinal)
        || customId.StartsWith(EditTimePrefix, StringComparison.Ordinal)
        || customId.StartsWith(EditTimeModalPrefix, StringComparison.Ordinal)
        || customId.StartsWith(ScrambleTeamsPrefix, StringComparison.Ordinal)
        || customId.StartsWith(MovePlayersPrefix, StringComparison.Ordinal)
        || customId.StartsWith(VoiceChannelSelectPrefix, StringComparison.Ordinal)
        || customId.StartsWith(ReadyPrefix, StringComparison.Ordinal)
        || customId.StartsWith(NotReadyPrefix, StringComparison.Ordinal)
        || customId.StartsWith(StartPrefix, StringComparison.Ordinal)
        || customId.StartsWith(ConfirmStartPrefix, StringComparison.Ordinal)
        || customId.StartsWith(CancelStartPrefix, StringComparison.Ordinal)
        || customId.StartsWith(ClosePrefix, StringComparison.Ordinal)
        || customId.StartsWith(ConfirmClosePrefix, StringComparison.Ordinal)
        || customId.StartsWith(CancelClosePrefix, StringComparison.Ordinal);

}
