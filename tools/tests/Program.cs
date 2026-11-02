using Coop.Session;

var ids = new HashSet<string>();
int checks = 0;
void Expect(bool condition, string description)
{
    if (!condition) throw new Exception(description);
    Console.WriteLine("PASS " + description);
    checks++;
}
JoinPayload Profile(string id = "a", string version = ApprovalPolicy.BuildVersion, string name = "Player") =>
    new() { playerId = id, version = version, name = name };

Expect(ApprovalPolicy.RejectReason(Profile(), 0, ids) == "", "valid first player accepted");
Expect(ApprovalPolicy.RejectReason(null!, 0, ids).Contains("Invalid"), "missing profile rejected");
Expect(ApprovalPolicy.RejectReason(Profile(name: "  "), 0, ids).Contains("Invalid"), "blank name rejected");
Expect(ApprovalPolicy.RejectReason(Profile(id: new string('x', 65)), 0, ids).Contains("Invalid"), "oversized identity rejected");
Expect(ApprovalPolicy.RejectReason(Profile(name: new string('x', 25)), 0, ids).Contains("Invalid"), "oversized display name rejected");
Expect(ApprovalPolicy.RejectReason(Profile(name: new string('\u754c', 24)), 0, ids).Contains("too long"), "UTF-8 byte capacity enforced before FixedString construction");
Expect(ApprovalPolicy.RejectReason(Profile(version: "future"), 0, ids).Contains("Different build"), "version mismatch has actionable reason");

// Simulate approval callbacks in their real single-threaded order. Pending approvals
// reserve their slots immediately, before their clients finish scene synchronization.
for (int i = 0; i < 4; i++)
{
    var profile = Profile(i.ToString());
    Expect(ApprovalPolicy.RejectReason(profile, ids.Count, ids) == "", $"reservation {i + 1} accepted");
    ids.Add(profile.playerId);
}
Expect(ApprovalPolicy.RejectReason(Profile("fifth"), ids.Count, ids).Contains("full"), "fifth pending or connected player rejected");
Expect(ApprovalPolicy.RejectReason(Profile("2"), ids.Count, ids).Contains("already"), "duplicate identity has a specific reason even when full");
ids.Remove("2");
Expect(ApprovalPolicy.RejectReason(Profile("2"), ids.Count, ids) == "", "disconnected identity may rejoin");
Expect(ApprovalPolicy.RejectReason(Profile("replacement"), ids.Count, ids) == "", "released reservation makes one slot available");
var barrier = new LoadingBarrier();
barrier.Begin(new ulong[] { 0, 1, 2 });
barrier.MarkReady(0);
barrier.MarkReady(1);
Expect(!barrier.TryOpen(), "initial level waits for every participant");
barrier.Remove(2);
Expect(barrier.TryOpen(), "disconnected loading client no longer blocks remaining players");
barrier.Add(3);
Expect(!barrier.IsLoading && !barrier.TryOpen(), "late join does not restart the global barrier");
barrier.Begin(new ulong[] { 0, 1 });
barrier.MarkReady(0);
barrier.Add(2);
barrier.MarkReady(1);
Expect(!barrier.TryOpen(), "join during initial load participates in the barrier");
barrier.MarkReady(2);
barrier.MarkReady(2);
Expect(barrier.TryOpen(), "duplicate load notifications are idempotent");
Expect(!barrier.TryOpen(), "barrier opens only once");
barrier.Begin(Array.Empty<ulong>());
Expect(!barrier.TryOpen(), "empty session never starts gameplay");
barrier.Begin(new ulong[] { 0 });
barrier.MarkReady(77);
Expect(!barrier.TryOpen(), "unknown client cannot complete another player's load");
barrier.Reset();
Expect(!barrier.IsLoading && !barrier.TryOpen(), "shutdown resets loading state");
Console.WriteLine($"All {checks} session policy checks passed.");
