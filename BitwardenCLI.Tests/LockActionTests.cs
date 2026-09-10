using BarRaider.SdTools;
using NSubstitute;

namespace BitwardenStreamdeckPlugin.Tests;

public class LockActionTests
{
    [Fact]
    public async Task Locking_invokes_bw_lock_and_confirms()
    {
        ISDConnection connection = Substitute.For<ISDConnection>();
        IBwCli cli = Substitute.For<IBwCli>();
        cli.Run(Arg.Any<string[]>()).Returns("Your vault is locked.");

        var action = new Lock(connection, TestPayloads.Empty(), cli);

        await action.LockVault();

        await cli.Received(1).Run("lock");
        await connection.Received(1).ShowOk();
    }

    [Fact]
    public async Task A_failing_lock_alerts_instead_of_reporting_success()
    {
        ISDConnection connection = Substitute.For<ISDConnection>();
        IBwCli cli = Substitute.For<IBwCli>();
        cli.Run(Arg.Any<string[]>())
            .Returns<string>(_ => throw new BwCliException("not logged in", 1));

        var action = new Lock(connection, TestPayloads.Empty(), cli);

        await action.LockVault();

        await connection.Received(1).ShowAlert();
        await connection.DidNotReceive().ShowOk();
    }
}
