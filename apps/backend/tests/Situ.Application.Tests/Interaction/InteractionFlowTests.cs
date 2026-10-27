using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Features.Interaction.AccountDeletion;
using Situ.Application.Features.Interaction.Blocks;
using Situ.Application.Features.Interaction.Comments;
using Situ.Application.Features.Interaction.Complaints;
using Situ.Application.Features.Interaction.Reactions;
using Situ.Application.Features.Reports.GetReport;
using Situ.Application.Features.Reports.GetReportMap;
using Situ.Application.Tests.Support;
using Situ.Domain.Administration;
using Situ.Domain.Common;
using Situ.Domain.Geography;
using Situ.Domain.Guardianship;
using Situ.Domain.Identity;
using Situ.Domain.Moderation;
using Situ.Domain.News;
using Situ.Domain.Notifications;
using Situ.Domain.Profiles;
using Situ.Domain.Reports;
using Situ.Domain.Retention;
using Situ.Domain.Social;
using Situ.Infrastructure.Reports;

namespace Situ.Application.Tests.Interaction;

public class InteractionFlowTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Anonymous_comments_keep_one_number_per_person_inside_a_report()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var author = await UserAsync(db, clock, 30, "author@situ.test", "author");
        var first = await UserAsync(db, clock, 30, "one@situ.test", "one");
        var second = await UserAsync(db, clock, 30, "two@situ.test", "two");
        var report = await ReportAsync(db, author, ContentVisibility.Public, anonymous: true);
        var other = await ReportAsync(db, author, ContentVisibility.Public, anonymous: true);

        var again = await new AddCommentHandler(db, clock).Handle(new AddCommentRequest(first.Id, report.Id, "Sigue el trancón.", true, null), CancellationToken.None);
        var firstComment = await new AddCommentHandler(db, clock).Handle(new AddCommentRequest(first.Id, report.Id, "En la misma esquina.", true, null), CancellationToken.None);
        var otherPerson = await new AddCommentHandler(db, clock).Handle(new AddCommentRequest(second.Id, report.Id, "Yo también lo vi.", true, null), CancellationToken.None);
        var otherReport = await new AddCommentHandler(db, clock).Handle(new AddCommentRequest(first.Id, other.Id, "Acá es otro caso.", true, null), CancellationToken.None);

        Assert.True(firstComment.IsSuccess);
        Assert.Equal("Situero anónimo 1", firstComment.Value!.Author.DisplayName);
        Assert.Equal("Situero anónimo 1", again.Value!.Author.DisplayName);
        Assert.Null(firstComment.Value.Author.UserId);
        Assert.Equal("Situero anónimo 2", otherPerson.Value!.Author.DisplayName);
        Assert.Equal("Situero anónimo 1", otherReport.Value!.Author.DisplayName);
        Assert.Equal(2, await db.AnonymousParticipants.CountAsync(item => item.TargetId == report.Id));
        Assert.Equal(3, report.CommentCount);

        var listed = await new ListCommentsHandler(db, clock).Handle(new ListCommentsRequest(second.Id, report.Id, null), CancellationToken.None);
        Assert.Equal(3, listed.Value!.Comments.Count);
        Assert.All(listed.Value.Comments, item => Assert.Null(item.Author.UserId));
        Assert.Contains(listed.Value.Comments, item => item.Author.DisplayName == "Situero anónimo 1");
        Assert.Contains(listed.Value.Comments, item => item.Author.DisplayName == "Situero anónimo 2");
    }

    [Fact]
    public async Task Simultaneous_anonymous_comments_do_not_share_a_number()
    {
        var name = Guid.NewGuid().ToString();
        await using var seed = IdentityTestDb.Create(name);
        var clock = new FixedClock(Now);
        var author = await UserAsync(seed, clock, 30, "seed-author@situ.test", "seedauthor");
        var report = await ReportAsync(seed, author, ContentVisibility.Public, false);
        var commenters = new List<User>();
        for (var i = 0; i < 8; i++)
        {
            commenters.Add(await UserAsync(seed, clock, 30, $"race{i}@situ.test", $"race{i}"));
        }

        var indexes = await Task.WhenAll(commenters.Select(async user =>
        {
            await using var db = IdentityTestDb.Create(name);
            var result = await new AddCommentHandler(db, clock).Handle(
                new AddCommentRequest(user.Id, report.Id, "Comentario al mismo tiempo.", true, null),
                CancellationToken.None);
            Assert.True(result.IsSuccess);
            return result.Value!.Author.DisplayName;
        }));

        Assert.Equal(8, indexes.Distinct().Count());
        Assert.Equal(Enumerable.Range(1, 8).Select(i => $"Situero anónimo {i}").OrderBy(item => item), indexes.OrderBy(item => item));
    }

    [Fact]
    public async Task A_minor_cannot_comment_with_their_name_on_a_public_report()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var author = await UserAsync(db, clock, 30, "pub@situ.test", "publicauthor");
        var minor = await UserAsync(db, clock, 17, "minor@situ.test", "minoruser");
        var adult = await UserAsync(db, clock, 30, "adult@situ.test", "adultuser");
        var publicReport = await ReportAsync(db, author, ContentVisibility.Public, false);
        var friendsReport = await ReportAsync(db, author, ContentVisibility.FriendsOnly, false);
        var friendship = Friendship.Request(author.Id, minor.Id, Now);
        friendship.Accept(minor.Id, Now);
        db.Friendships.Add(friendship);
        await db.SaveChangesAsync();

        var named = await new AddCommentHandler(db, clock).Handle(new AddCommentRequest(minor.Id, publicReport.Id, "Voy con mi nombre.", false, null), CancellationToken.None);
        var anonymous = await new AddCommentHandler(db, clock).Handle(new AddCommentRequest(minor.Id, publicReport.Id, "Voy anónimo.", true, null), CancellationToken.None);
        var friends = await new AddCommentHandler(db, clock).Handle(new AddCommentRequest(minor.Id, friendsReport.Id, "Esto es entre amigos.", false, null), CancellationToken.None);
        var adultNamed = await new AddCommentHandler(db, clock).Handle(new AddCommentRequest(adult.Id, publicReport.Id, "Yo soy mayor.", false, null), CancellationToken.None);

        Assert.Equal("comment.minor_public_identity", named.Error!.Code);
        Assert.True(anonymous.IsSuccess);
        Assert.Null(anonymous.Value!.Author.UserId);
        Assert.True(friends.IsSuccess);
        Assert.Equal(minor.Id, friends.Value!.Author.UserId);
        Assert.True(adultNamed.IsSuccess);
    }

    [Fact]
    public async Task Comments_follow_report_visibility_blocks_and_closed_states()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var author = await UserAsync(db, clock, 30, "owner@situ.test", "owner");
        var friend = await UserAsync(db, clock, 30, "friend@situ.test", "friend");
        var stranger = await UserAsync(db, clock, 30, "stranger@situ.test", "stranger");
        var blocked = await UserAsync(db, clock, 30, "blocked@situ.test", "blocked");
        var report = await ReportAsync(db, author, ContentVisibility.FriendsOnly, false);
        var friendship = Friendship.Request(author.Id, friend.Id, Now);
        friendship.Accept(friend.Id, Now);
        db.Friendships.Add(friendship);
        await db.SaveChangesAsync();

        var comment = await new AddCommentHandler(db, clock).Handle(new AddCommentRequest(friend.Id, report.Id, "Solo amigos.", false, null), CancellationToken.None);
        var reply = await new AddCommentHandler(db, clock).Handle(new AddCommentRequest(author.Id, report.Id, "Gracias.", false, comment.Value!.Id), CancellationToken.None);
        Assert.True(reply.IsSuccess);
        Assert.Equal(2, report.CommentCount);

        var hidden = await new ListCommentsHandler(db, clock).Handle(new ListCommentsRequest(stranger.Id, report.Id, null), CancellationToken.None);
        Assert.Equal("report.not_found", hidden.Error!.Code);
        var seen = await new ListCommentsHandler(db, clock).Handle(new ListCommentsRequest(friend.Id, report.Id, null), CancellationToken.None);
        var parent = Assert.Single(seen.Value!.Comments);
        Assert.Equal("Gracias.", Assert.Single(parent.Replies).Text);

        db.UserBlocks.Add(UserBlock.Create(author.Id, blocked.Id, Now));
        await db.SaveChangesAsync();
        var blockedComment = await new AddCommentHandler(db, clock).Handle(new AddCommentRequest(blocked.Id, report.Id, "No debería verse.", false, null), CancellationToken.None);
        Assert.Equal("report.not_found", blockedComment.Error!.Code);

        var expired = await ReportAsync(db, author, ContentVisibility.Public, false, TimeSpan.FromMinutes(1));
        clock.UtcNow = Now.AddMinutes(2);
        var late = await new AddCommentHandler(db, clock).Handle(new AddCommentRequest(stranger.Id, expired.Id, "Tarde.", false, null), CancellationToken.None);
        Assert.Equal("comment.report_closed", late.Error!.Code);

        clock.UtcNow = Now;
        var removed = await new DeleteCommentHandler(db, clock).Handle(new DeleteCommentRequest(friend.Id, comment.Value.Id), CancellationToken.None);
        Assert.True(removed.IsSuccess);
        Assert.Equal(0, report.CommentCount);
        Assert.Equal(CommentStatus.Removed, (await db.Comments.FirstAsync(item => item.Id == reply.Value!.Id)).Status);
    }

    [Fact]
    public async Task A_user_has_one_reaction_and_counts_stay_in_sync()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var author = await UserAsync(db, clock, 30, "react-author@situ.test", "reactauthor");
        var voter = await UserAsync(db, clock, 30, "voter@situ.test", "voter");
        var other = await UserAsync(db, clock, 30, "other-voter@situ.test", "othervoter");
        var report = await ReportAsync(db, author, ContentVisibility.Public, false);
        var comment = await new AddCommentHandler(db, clock).Handle(new AddCommentRequest(voter.Id, report.Id, "Interesante.", false, null), CancellationToken.None);

        var like = await new SetReactionHandler(db, clock).Handle(new SetReactionRequest(voter.Id, ContentTargetType.Report, report.Id, ReactionType.Like), CancellationToken.None);
        var love = await new SetReactionHandler(db, clock).Handle(new SetReactionRequest(voter.Id, ContentTargetType.Report, report.Id, ReactionType.Love), CancellationToken.None);
        await new SetReactionHandler(db, clock).Handle(new SetReactionRequest(other.Id, ContentTargetType.Report, report.Id, ReactionType.Like), CancellationToken.None);

        Assert.Equal("Love", love.Value!.Type);
        Assert.Equal(1, love.Value.Counts.Sum(item => item.Count));
        Assert.Equal("Love", Assert.Single(love.Value.Counts).Type);
        Assert.Equal(1, await db.Reactions.CountAsync(item => item.UserId == voter.Id && item.TargetId == report.Id));
        Assert.Equal(2, report.ReactionCount);
        Assert.True(like.IsSuccess);

        var detail = await new GetReportHandler(db, clock, new FakeSigner()).Handle(new GetReportRequest(voter.Id, report.Id), CancellationToken.None);
        Assert.Equal("Love", detail.Value!.Report.MyReaction);

        var removed = await new RemoveReactionHandler(db, clock).Handle(new RemoveReactionRequest(voter.Id, ContentTargetType.Report, report.Id), CancellationToken.None);
        Assert.Equal(1, removed.Value!.Counts.Sum(item => item.Count));
        Assert.Equal(1, report.ReactionCount);

        var onComment = await new SetReactionHandler(db, clock).Handle(new SetReactionRequest(author.Id, ContentTargetType.Comment, comment.Value!.Id, ReactionType.Wow), CancellationToken.None);
        Assert.Equal("Wow", Assert.Single(onComment.Value!.Counts).Type);
        Assert.Equal(1, report.ReactionCount);
    }

    [Fact]
    public async Task Complaints_hide_content_and_staff_can_uphold_or_restore_it()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var author = await UserAsync(db, clock, 30, "target@situ.test", "target");
        var reporter = await UserAsync(db, clock, 30, "reporter@situ.test", "reporter");
        var second = await UserAsync(db, clock, 30, "second@situ.test", "second");
        var third = await UserAsync(db, clock, 30, "third@situ.test", "third");
        var staff = await UserAsync(db, clock, 30, "staff@situ.test", "staff");
        var moderator = await UserAsync(db, clock, 30, "mod@situ.test", "moderator");
        await GrantAsync(db, staff, StaffPermissionCodes.ComplaintsResolve);
        await GrantAsync(db, moderator, StaffPermissionCodes.ComplaintsResolve, StaffPermissionCodes.UsersSuspend);
        var report = await ReportAsync(db, author, ContentVisibility.Public, true);
        var comment = await new AddCommentHandler(db, clock).Handle(new AddCommentRequest(author.Id, report.Id, "Texto anónimo.", true, null), CancellationToken.None);

        var self = await new FileComplaintHandler(db, clock).Handle(new FileComplaintRequest(author.Id, ContentTargetType.Report, report.Id, ComplaintReason.Spam, null), CancellationToken.None);
        Assert.Equal("complaint.self", self.Error!.Code);

        var first = await new FileComplaintHandler(db, clock).Handle(new FileComplaintRequest(reporter.Id, ContentTargetType.Comment, comment.Value!.Id, ComplaintReason.Spam, null), CancellationToken.None);
        var duplicate = await new FileComplaintHandler(db, clock).Handle(new FileComplaintRequest(reporter.Id, ContentTargetType.Comment, comment.Value.Id, ComplaintReason.Spam, null), CancellationToken.None);
        Assert.Equal("complaint.duplicate", duplicate.Error!.Code);
        Assert.Equal(CommentStatus.Visible, (await db.Comments.FirstAsync(item => item.Id == comment.Value.Id)).Status);

        await new FileComplaintHandler(db, clock).Handle(new FileComplaintRequest(second.Id, ContentTargetType.Comment, comment.Value.Id, ComplaintReason.Spam, null), CancellationToken.None);
        await new FileComplaintHandler(db, clock).Handle(new FileComplaintRequest(third.Id, ContentTargetType.Comment, comment.Value.Id, ComplaintReason.Spam, null), CancellationToken.None);
        Assert.Equal(CommentStatus.Hidden, (await db.Comments.FirstAsync(item => item.Id == comment.Value.Id)).Status);
        Assert.Equal(0, report.CommentCount);

        var priorityReport = await ReportAsync(db, author, ContentVisibility.Public, true);
        var priority = await new FileComplaintHandler(db, clock).Handle(new FileComplaintRequest(reporter.Id, ContentTargetType.Report, priorityReport.Id, ComplaintReason.MinorSafety, "Hay un menor en riesgo."), CancellationToken.None);
        Assert.True(priority.Value!.IsPriority);
        Assert.Equal(ReportStatus.UnderReview, priorityReport.Status);

        var forbidden = await new ListComplaintsHandler(db).Handle(new ListComplaintsRequest(reporter.Id, null, null), CancellationToken.None);
        Assert.Equal("complaint.forbidden", forbidden.Error!.Code);
        var queue = await new ListComplaintsHandler(db).Handle(new ListComplaintsRequest(staff.Id, "Open", null), CancellationToken.None);
        Assert.Equal(priority.Value.Id, queue.Value!.Complaints[0].Id);
        Assert.DoesNotContain(JsonSerializer.Serialize(queue.Value), author.Id.ToString());

        var started = await new StartComplaintReviewHandler(db, clock).Handle(new StartComplaintReviewRequest(staff.Id, first.Value!.Id), CancellationToken.None);
        Assert.True(started.IsSuccess);

        var noSuspend = await new UpholdComplaintHandler(db, clock).Handle(new UpholdComplaintRequest(staff.Id, priority.Value.Id, "Se confirma el riesgo.", "RemoveAndSuspend"), CancellationToken.None);
        Assert.Equal("users.suspend_forbidden", noSuspend.Error!.Code);
        Assert.Equal(ReportStatus.UnderReview, priorityReport.Status);

        var upheld = await new UpholdComplaintHandler(db, clock).Handle(new UpholdComplaintRequest(moderator.Id, priority.Value.Id, "Se retira el reporte.", "RemoveAndSuspend"), CancellationToken.None);
        Assert.True(upheld.IsSuccess);
        Assert.Equal(ReportStatus.Removed, priorityReport.Status);
        Assert.Equal(UserStatus.Suspended, (await db.Users.FirstAsync(item => item.Id == author.Id)).Status);
        Assert.Equal(4.3, (await db.UserReputations.FirstAsync(item => item.UserId == author.Id)).Score, 3);
        Assert.Contains(db.ModerationActions, item => item.Action == ModerationActionType.RemoveContent && item.TargetId == priorityReport.Id);
        Assert.Contains(db.AuditLogs, item => item.Action == "complaint.uphold");

        var spam = await db.Complaints.Where(item => item.TargetId == comment.Value.Id).ToListAsync();
        foreach (var complaint in spam)
        {
            await new DismissComplaintHandler(db, clock).Handle(new DismissComplaintRequest(staff.Id, complaint.Id, "No procede."), CancellationToken.None);
        }

        Assert.Equal(CommentStatus.Visible, (await db.Comments.FirstAsync(item => item.Id == comment.Value.Id)).Status);
        Assert.Equal(1, report.CommentCount);
        Assert.Contains(db.AuditLogs, item => item.Action == "complaint.dismiss");
    }

    [Fact]
    public async Task Anonymous_identity_is_revealed_only_with_permission_and_a_written_log()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var author = await UserAsync(db, clock, 30, "hidden@situ.test", "hidden");
        var reporter = await UserAsync(db, clock, 30, "denuncia@situ.test", "denuncia");
        var staff = await UserAsync(db, clock, 30, "reveal@situ.test", "reveal");
        await GrantAsync(db, staff, StaffPermissionCodes.AnonymityReveal, StaffPermissionCodes.ComplaintsResolve);
        var report = await ReportAsync(db, author, ContentVisibility.Public, true);
        var complaint = await new FileComplaintHandler(db, clock).Handle(new FileComplaintRequest(reporter.Id, ContentTargetType.Report, report.Id, ComplaintReason.Harassment, null), CancellationToken.None);

        var denied = await new RevealAnonymousIdentityHandler(db, clock).Handle(
            new RevealAnonymousIdentityRequest(reporter.Id, ContentTargetType.Report, report.Id, complaint.Value!.Id, "Quiero saber quién es."),
            CancellationToken.None);
        Assert.Equal("anonymity.forbidden", denied.Error!.Code);

        var shortReason = await new RevealAnonymousIdentityHandler(db, clock).Handle(
            new RevealAnonymousIdentityRequest(staff.Id, ContentTargetType.Report, report.Id, null, "muy corta"),
            CancellationToken.None);
        Assert.Equal("anonymity.reveal_denied", shortReason.Error!.Code);

        var revealed = await new RevealAnonymousIdentityHandler(db, clock).Handle(
            new RevealAnonymousIdentityRequest(staff.Id, ContentTargetType.Report, report.Id, complaint.Value.Id, "La denuncia exige identificar al autor real."),
            CancellationToken.None);
        Assert.Equal(author.Id, revealed.Value!.UserId);
        Assert.Equal(1, await db.AnonymousIdentityAccessLogs.CountAsync());
        Assert.Contains(db.AuditLogs, item => item.Action == "anonymity.reveal");

        var options = new DbContextOptionsBuilder<IdentityTestDb>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var failing = new FailingSaveDb(options);
        failing.Database.EnsureCreated();
        var failingClock = new FixedClock(Now);
        var failingAuthor = await UserAsync(failing, failingClock, 30, "fail@situ.test", "failhidden");
        var failingStaff = await UserAsync(failing, failingClock, 30, "failstaff@situ.test", "failstaff");
        await GrantAsync(failing, failingStaff, StaffPermissionCodes.AnonymityReveal);
        var failingReport = await ReportAsync(failing, failingAuthor, ContentVisibility.Public, true);
        failing.FailNext = true;
        var failed = await new RevealAnonymousIdentityHandler(failing, failingClock).Handle(
            new RevealAnonymousIdentityRequest(failingStaff.Id, ContentTargetType.Report, failingReport.Id, null, "Justificación larga para un acceso que no debe guardarse."),
            CancellationToken.None);
        Assert.Equal("anonymity.log_failed", failed.Error!.Code);
        Assert.False(failed.IsSuccess);
        Assert.Equal(0, await failing.AnonymousIdentityAccessLogs.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task Public_responses_do_not_include_an_anonymous_author_id()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var author = await UserAsync(db, clock, 30, "anon@situ.test", "anon");
        var viewer = await UserAsync(db, clock, 30, "viewer@situ.test", "viewer");
        var staff = await UserAsync(db, clock, 30, "queue@situ.test", "queue");
        await GrantAsync(db, staff, StaffPermissionCodes.ComplaintsResolve, StaffPermissionCodes.AnonymityReveal);
        var report = await ReportAsync(db, author, ContentVisibility.Public, true);
        var comment = await new AddCommentHandler(db, clock).Handle(new AddCommentRequest(author.Id, report.Id, "Sin nombre.", true, null), CancellationToken.None);
        var edited = await new EditCommentHandler(db, clock).Handle(new EditCommentRequest(author.Id, comment.Value!.Id, "Sin nombre, corregido."), CancellationToken.None);
        var listed = await new ListCommentsHandler(db, clock).Handle(new ListCommentsRequest(viewer.Id, report.Id, null), CancellationToken.None);
        var reaction = await new SetReactionHandler(db, clock).Handle(new SetReactionRequest(viewer.Id, ContentTargetType.Comment, comment.Value.Id, ReactionType.Like), CancellationToken.None);
        var complaint = await new FileComplaintHandler(db, clock).Handle(new FileComplaintRequest(viewer.Id, ContentTargetType.Comment, comment.Value.Id, ComplaintReason.Spam, null), CancellationToken.None);
        var queue = await new ListComplaintsHandler(db).Handle(new ListComplaintsRequest(staff.Id, null, null), CancellationToken.None);
        var blocked = await new BlockContentAuthorHandler(db, clock).Handle(new BlockContentAuthorRequest(viewer.Id, ContentTargetType.Comment, comment.Value.Id), CancellationToken.None);
        var detail = await new GetReportHandler(db, clock, new FakeSigner()).Handle(new GetReportRequest(viewer.Id, report.Id), CancellationToken.None);

        var payloads = new[]
        {
            JsonSerializer.Serialize(comment.Value),
            JsonSerializer.Serialize(edited.Value),
            JsonSerializer.Serialize(listed.Value),
            JsonSerializer.Serialize(reaction.Value),
            JsonSerializer.Serialize(complaint.Value),
            JsonSerializer.Serialize(queue.Value),
            JsonSerializer.Serialize(blocked.Value),
            JsonSerializer.Serialize(detail.Value),
        };
        Assert.All(payloads, payload => Assert.DoesNotContain(author.Id.ToString(), payload));
        Assert.True(blocked.Value!.Blocked);

        var revealed = await new RevealAnonymousIdentityHandler(db, clock).Handle(
            new RevealAnonymousIdentityRequest(staff.Id, ContentTargetType.Comment, comment.Value.Id, complaint.Value!.Id, "La cola de denuncias necesita el autor."),
            CancellationToken.None);
        Assert.Contains(author.Id.ToString(), JsonSerializer.Serialize(revealed.Value));
    }

    [Fact]
    public async Task Blocking_hides_the_other_person_and_ends_the_friendship()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var author = await UserAsync(db, clock, 30, "map-author@situ.test", "mapauthor");
        var viewer = await UserAsync(db, clock, 30, "map-viewer@situ.test", "mapviewer");
        var profile = UserProfile.CreateDefault(author.Id, "Ana");
        profile.MarkCreated(Now);
        profile.EnableFriendLocationSharing(LocationSharingMode.AllFriends, null, Now);
        var viewerProfile = UserProfile.CreateDefault(viewer.Id, "Luis");
        viewerProfile.MarkCreated(Now);
        viewerProfile.EnableFriendLocationSharing(LocationSharingMode.AllFriends, null, Now);
        db.UserProfiles.AddRange(profile, viewerProfile);
        var friendship = Friendship.Request(author.Id, viewer.Id, Now);
        friendship.Accept(viewer.Id, Now);
        db.Friendships.Add(friendship);
        db.LocationShareRecipients.Add(LocationShareRecipient.Create(author.Id, viewer.Id));
        await db.SaveChangesAsync();
        var report = await ReportAsync(db, author, ContentVisibility.Public, true);
        var comment = await new AddCommentHandler(db, clock).Handle(new AddCommentRequest(author.Id, report.Id, "Anónimo en el mapa.", true, null), CancellationToken.None);

        var named = await ReportAsync(db, author, ContentVisibility.Public, false);
        var namedComment = await new AddCommentHandler(db, clock).Handle(new AddCommentRequest(author.Id, named.Id, "Voy con mi nombre.", false, null), CancellationToken.None);
        Assert.True(namedComment.IsSuccess);

        var block = await new BlockContentAuthorHandler(db, clock).Handle(new BlockContentAuthorRequest(viewer.Id, ContentTargetType.Report, report.Id), CancellationToken.None);
        var again = await new BlockContentAuthorHandler(db, clock).Handle(new BlockContentAuthorRequest(viewer.Id, ContentTargetType.Comment, comment.Value!.Id), CancellationToken.None);
        Assert.Equal(JsonSerializer.Serialize(block.Value), JsonSerializer.Serialize(again.Value));
        Assert.Equal(1, await db.UserBlocks.CountAsync(item => item.BlockerId == viewer.Id && item.Scope == UserBlockScope.AnonymousContentOnly));
        Assert.DoesNotContain(author.Id.ToString(), JsonSerializer.Serialize(block.Value));
        Assert.Equal(FriendshipStatus.Accepted, friendship.Status);
        Assert.Single(db.LocationShareRecipients);
        Assert.True(profile.ShareLocationWithFriends);

        var map = new GetReportMapHandler(db, clock, new ReportMapReader(db, clock), new FakeSigner());
        var hidden = await map.Handle(new GetReportMapRequest(viewer.Id, 4.5, -74.3, 4.9, -73.9, 16, [], null, null, null, null), CancellationToken.None);
        Assert.DoesNotContain(hidden.Value!.Items, item => item.Id == report.Id);
        Assert.Contains(hidden.Value.Items, item => item.Id == named.Id);
        var asAuthor = await map.Handle(new GetReportMapRequest(author.Id, 4.5, -74.3, 4.9, -73.9, 16, [], null, null, null, null), CancellationToken.None);
        Assert.Contains(asAuthor.Value!.Items, item => item.Id == report.Id);

        var comments = await new ListCommentsHandler(db, clock).Handle(new ListCommentsRequest(viewer.Id, named.Id, null), CancellationToken.None);
        Assert.Contains(comments.Value!.Comments, item => item.Id == namedComment.Value!.Id);
        var anonymousComments = await new ListCommentsHandler(db, clock).Handle(new ListCommentsRequest(viewer.Id, report.Id, null), CancellationToken.None);
        Assert.Equal("report.not_found", anonymousComments.Error!.Code);

        var listed = await new ListBlocksHandler(db).Handle(new ListBlocksRequest(viewer.Id), CancellationToken.None);
        var anonymousEntry = Assert.Single(listed.Value!.Blocks);
        var listedJson = JsonSerializer.Serialize(listed.Value);
        Assert.DoesNotContain(author.Id.ToString(), listedJson);
        Assert.DoesNotContain(author.UserName.Value, listedJson);
        Assert.DoesNotContain("Ana", listedJson);
        Assert.Equal("Usuario anónimo", anonymousEntry.DisplayName);
        Assert.Null(anonymousEntry.UserId);
        Assert.StartsWith("desde un reporte del", anonymousEntry.OriginLabel);

        var known = await new BlockUserHandler(db, clock).Handle(new BlockUserRequest(author.Id, viewer.Id), CancellationToken.None);
        var fullList = await new ListBlocksHandler(db).Handle(new ListBlocksRequest(author.Id), CancellationToken.None);
        Assert.Equal(viewer.Id, Assert.Single(fullList.Value!.Blocks).UserId);
        Assert.Equal(FriendshipStatus.Cancelled, friendship.Status);
        Assert.True(known.IsSuccess);
        var removed = await new UnblockUserHandler(db).Handle(new UnblockUserRequest(viewer.Id, anonymousEntry.BlockId), CancellationToken.None);
        Assert.True(removed.IsSuccess);
    }

    [Fact]
    public async Task Account_deletion_closes_sessions_can_be_cancelled_and_erases_personal_data()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var user = await UserAsync(db, clock, 16, "erase@situ.test", "erase");
        user.GrantGuardianConsent(Now);
        var consent = GuardianConsent.Request(user.Id, "tutor@situ.test", "token-hash", "v1", Now, Now);
        db.GuardianConsents.Add(consent);
        var device = UserDevice.Register(user.Id, "device-1", DevicePlatform.IOS, "iPhone", "1.0", "127.0.0.1", Now);
        device.UpdatePushToken("push-token");
        db.UserDevices.Add(device);
        db.DeviceAlertLocations.Add(DeviceAlertLocation.Record(user.Id, device.Id, 4.71149, -74.07249, 30, Now, TimeSpan.FromMinutes(30)));
        db.RefreshTokens.Add(RefreshToken.Issue(user.Id, device.Id, "refresh-hash", Now, TimeSpan.FromDays(30)));
        var report = await ReportAsync(db, user, ContentVisibility.FriendsOnly, false);
        var media = ReportMedia.Create(report.Id, user.Id, MediaType.Image, "media/original", 1200, null, 0, false, Now);
        media.MarkReady("media/thumb");
        db.ReportMedias.Add(media);
        var profile = UserProfile.CreateDefault(user.Id, "Ana");
        profile.MarkCreated(Now);
        db.UserProfiles.Add(profile);
        await db.SaveChangesAsync();
        var files = new MemoryFiles();
        var mail = new FakeEmailSender();
        var passwords = new PlainHasher();

        var wrong = await new RequestAccountDeletionHandler(db, clock, passwords, mail).Handle(new RequestAccountDeletionRequest(user.Id, "no"), CancellationToken.None);
        Assert.Equal("account.password_invalid", wrong.Error!.Code);
        Assert.Equal(UserStatus.Active, user.Status);

        var requested = await new RequestAccountDeletionHandler(db, clock, passwords, mail).Handle(new RequestAccountDeletionRequest(user.Id, "hash"), CancellationToken.None);
        Assert.Equal(UserStatus.DeletionRequested, user.Status);
        Assert.NotNull(device.RevokedAt);
        Assert.Null(device.PushToken);
        Assert.NotNull((await db.RefreshTokens.SingleAsync()).RevokedAt);
        Assert.Contains("cancelar", mail.Messages.Single().Body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(requested.Value!.ScheduledFor.ToString("yyyy-MM-dd"), mail.Messages.Single().Body);

        var cancelled = await new CancelAccountDeletionHandler(db, clock).Handle(new CancelAccountDeletionRequest(user.Id), CancellationToken.None);
        Assert.True(cancelled.IsSuccess);
        Assert.Equal(UserStatus.Active, user.Status);

        await new RequestAccountDeletionHandler(db, clock, passwords, mail).Handle(new RequestAccountDeletionRequest(user.Id, "hash"), CancellationToken.None);
        var early = await new CompleteAccountDeletionsHandler(db, clock, files).Handle(new CompleteAccountDeletionsRequest(), CancellationToken.None);
        Assert.Equal(0, early.Value!.Completed);

        clock.UtcNow = requested.Value.ScheduledFor.AddDays(14);
        var done = await new CompleteAccountDeletionsHandler(db, clock, files).Handle(new CompleteAccountDeletionsRequest(), CancellationToken.None);
        Assert.Equal(1, done.Value!.Completed);
        Assert.Equal(UserStatus.Deleted, user.Status);
        Assert.EndsWith("@removed.invalid", user.Email.Value, StringComparison.Ordinal);
        Assert.Equal("Cuenta eliminada", profile.DisplayName);
        Assert.Equal("erased", user.PasswordHash);
        Assert.Empty(db.Reports);
        Assert.Empty(db.UserDevices);
        Assert.Empty(db.DeviceAlertLocations);
        Assert.Equal(new[] { "media/original", "media/thumb" }, files.Deleted);
        Assert.NotNull(await db.GuardianConsents.FirstOrDefaultAsync(item => item.Id == consent.Id));
        Assert.Contains(db.AuditLogs, item => item.Action == "account_deletion.completed");
    }

    [Fact]
    public async Task A_legal_hold_stops_account_deletion()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var user = await UserAsync(db, clock, 30, "held@situ.test", "held");
        var staff = await UserAsync(db, clock, 30, "legal@situ.test", "legal");
        var report = await ReportAsync(db, user, ContentVisibility.Public, false);
        db.LegalHolds.Add(LegalHold.Place(ContentTargetType.Report, report.Id, "Proceso en curso.", staff.Id, Now));
        await db.SaveChangesAsync();
        await new RequestAccountDeletionHandler(db, clock, new PlainHasher(), new FakeEmailSender()).Handle(new RequestAccountDeletionRequest(user.Id, "hash"), CancellationToken.None);
        clock.UtcNow = Now.AddDays(20);

        var result = await new CompleteAccountDeletionsHandler(db, clock, new MemoryFiles()).Handle(new CompleteAccountDeletionsRequest(), CancellationToken.None);

        Assert.Equal(1, result.Value!.BlockedByLegalHold);
        Assert.Equal(0, result.Value.Completed);
        Assert.Equal(UserStatus.DeletionRequested, user.Status);
        Assert.Equal("held@situ.test", user.Email.Value);
        Assert.NotNull(await db.Reports.FirstOrDefaultAsync(item => item.Id == report.Id));
        Assert.Equal(AccountDeletionStatus.BlockedByLegalHold, (await db.AccountDeletionRequests.SingleAsync()).Status);
        Assert.Contains(db.AuditLogs, item => item.Action == "account_deletion.blocked_by_legal_hold");
    }

    [Fact]
    public async Task Rejects_a_comment_that_uses_a_blocked_term()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var author = await UserAsync(db, clock, 30, "author@situ.test", "author");
        var report = await ReportAsync(db, author, ContentVisibility.Public, false);

        var result = await new AddCommentHandler(db, clock).Handle(
            new AddCommentRequest(author.Id, report.Id, "Esto es una mierda.", false, null),
            CancellationToken.None);

        Assert.Equal("content.not_allowed", result.Error!.Code);
    }

    private static async Task GrantAsync(IdentityTestDb db, User user, params string[] permissions)
    {
        var role = StaffRole.Create("Moderator", "Modera contenido.", true, Now);
        db.StaffRoles.Add(role);
        foreach (var permission in permissions)
        {
            db.StaffPermissions.Add(StaffPermission.Grant(role.Id, permission));
        }

        db.StaffAssignments.Add(StaffAssignment.Assign(user.Id, role.Id, null, Now));
        await db.SaveChangesAsync();
    }

    private static async Task<Report> ReportAsync(
        IdentityTestDb db,
        User author,
        ContentVisibility visibility,
        bool anonymous,
        TimeSpan? lifetime = null)
    {
        var report = Report.Create(
            author.Id,
            Guid.NewGuid(),
            author.CityId,
            new GeoPoint(4.711, -74.072),
            "Hay novedad en esta esquina ahora mismo.",
            anonymous,
            author.IsMinor(Now),
            lifetime ?? TimeSpan.FromHours(2),
            Now,
            visibility: visibility);
        db.Reports.Add(report);
        await db.SaveChangesAsync();
        return report;
    }

    private static async Task<User> UserAsync(IdentityTestDb db, FixedClock clock, int years, string email, string userName)
    {
        var city = await db.Cities.Select(item => new { item.Id, item.RegionId, item.CountryId }).FirstOrDefaultAsync();
        Guid countryId;
        Guid regionId;
        Guid cityId;
        if (city is null)
        {
            var country = Country.Create("CO", "Colombia", "Departamento", "es-CO", "COP", "America/Bogota", DistanceUnit.Kilometers, 14, true);
            var region = Region.Create(country.Id, "Cundinamarca", "25");
            var created = City.Create(region.Id, country.Id, "Bogotá", new GeoPoint(4.711, -74.072), "America/Bogota");
            db.AddRange(country, region, created);
            await db.SaveChangesAsync();
            countryId = country.Id;
            regionId = region.Id;
            cityId = created.Id;
        }
        else
        {
            countryId = city.CountryId;
            regionId = city.RegionId;
            cityId = city.Id;
        }

        var user = User.Register(
            Email.Create(email),
            UserName.Create(userName),
            PersonName.Create("Ana"),
            PersonName.Create("Pérez"),
            PhoneNumber.Create("+573001234567"),
            "hash",
            DateOnly.FromDateTime(clock.UtcNow.UtcDateTime).AddYears(-years),
            countryId,
            regionId,
            cityId,
            "es-CO",
            14,
            clock.UtcNow);
        user.ConfirmEmail(clock.UtcNow);
        if (user.IsMinor(clock.UtcNow))
        {
            user.GrantGuardianConsent(clock.UtcNow);
        }

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private sealed class PlainHasher : IPasswordHasher
    {
        public string Hash(string password) => password;

        public bool Verify(string password, string passwordHash) => password == passwordHash;
    }

    private sealed class MemoryFiles : IFileStorage
    {
        public List<string> Deleted { get; } = [];

        public Task SaveAsync(string key, byte[] content, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DeleteAsync(string key, CancellationToken cancellationToken)
        {
            Deleted.Add(key);
            return Task.CompletedTask;
        }

        public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken) => Task.FromResult<Stream?>(null);
    }

    private sealed class FakeSigner : IMediaUrlSigner
    {
        public (long ExpiresUnix, string Signature) Sign(Guid mediaId, DateTimeOffset expiresAt) => (expiresAt.ToUnixTimeSeconds(), "sig");

        public bool IsValid(Guid mediaId, long expiresUnix, string signature, DateTimeOffset now) => signature == "sig";
    }

    private sealed class FailingSaveDb(DbContextOptions<IdentityTestDb> options) : IdentityTestDb(options)
    {
        public bool FailNext { get; set; }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (FailNext)
            {
                FailNext = false;
                throw new DbUpdateException("The access log could not be written.");
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }
}
