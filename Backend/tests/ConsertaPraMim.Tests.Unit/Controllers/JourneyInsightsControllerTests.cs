using AppMobileCPM.Areas.Admin.Controllers;
using AppMobileCPM.Areas.Admin.ViewModels;
using AppMobileCPM.Integrations.Journey;
using AppMobileCPM.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Controllers;

public sealed class JourneyInsightsControllerTests
{
    [Fact(DisplayName = "JourneyInsightsController | Deve montar view com filtro de board, canal e governanca")]
    public void Index_DeveMontarViewComFiltroEFlags()
    {
        var kanbanServiceMock = new Mock<IAdminKanbanService>(MockBehavior.Strict);
        kanbanServiceMock
            .Setup(service => service.GetJourneyOperationsDashboard(
                It.Is<AdminKanbanJourneyOperationsDashboardFilter>(request =>
                    request.BoardType == AdminKanbanBoardTypes.Clients &&
                    request.SourceChannel == AdminKanbanJourneySourceChannels.Telegram &&
                    request.CreatedToUtcExclusive > request.CreatedFromUtc &&
                    request.BreakdownLimit == 8)))
            .Returns(new AdminKanbanJourneyOperationsDashboardSnapshot
            {
                ScopeBoardType = AdminKanbanBoardTypes.Clients,
                ScopeSourceChannel = AdminKanbanJourneySourceChannels.Telegram,
                CreatedFromUtc = new DateTime(2026, 3, 2, 3, 0, 0, DateTimeKind.Utc),
                CreatedToUtcExclusive = new DateTime(2026, 3, 17, 3, 0, 0, DateTimeKind.Utc),
                TotalJourneys = 18,
                CompletedJourneys = 7
            });

        var controller = new JourneyInsightsController(
            kanbanServiceMock.Object,
            Options.Create(new JourneyGovernanceOptions
            {
                Enabled = true,
                RolloutPercentage = 75,
                AllowedSourceChannels = "landing,telegram",
                IntakeEnabled = true,
                StageAutomationEnabled = true,
                MatchingEnabled = true,
                DispatchEnabled = true,
                ConnectionEnabled = true,
                ClosureEnabled = true,
                RouteOperationalExceptionsToHandoff = true
            }));

        var result = controller.Index(new AdminJourneyOperationsDashboardFilterInputModel
        {
            BoardType = AdminKanbanBoardTypes.Clients,
            SourceChannel = AdminKanbanJourneySourceChannels.Telegram,
            StartDate = new DateTime(2026, 3, 2),
            EndDate = new DateTime(2026, 3, 16)
        });

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AdminJourneyOperationsDashboardPageViewModel>(view.Model);
        Assert.Equal("Painel da jornada autonoma", model.Title);
        Assert.Equal(AdminKanbanBoardTypes.Clients, model.SelectedBoardType);
        Assert.Equal(AdminKanbanJourneySourceChannels.Telegram, model.SelectedSourceChannel);
        Assert.Equal(18, model.Snapshot.TotalJourneys);
        Assert.Equal(75, model.Governance.RolloutPercentage);

        kanbanServiceMock.VerifyAll();
    }
}
