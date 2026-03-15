using AppMobileCPM.Areas.Admin.Controllers;
using AppMobileCPM.Areas.Admin.ViewModels;
using AppMobileCPM.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Controllers;

public sealed class TelegramInsightsControllerTests
{
    [Fact(DisplayName = "TelegramInsightsController | Deve montar view com filtro de board e snapshot do painel")]
    public void Index_DeveMontarViewComFiltroDeBoardESnapshot()
    {
        var kanbanServiceMock = new Mock<IAdminKanbanService>(MockBehavior.Strict);
        kanbanServiceMock
            .Setup(service => service.GetTelegramBusinessDashboard(
                It.Is<AdminKanbanTelegramBusinessDashboardFilter>(request =>
                    request.BoardType == AdminKanbanBoardTypes.Clients &&
                    request.CreatedToUtcExclusive > request.CreatedFromUtc &&
                    request.BreakdownLimit == 8)))
            .Returns(new AdminKanbanTelegramBusinessDashboardSnapshot
            {
                ScopeBoardType = AdminKanbanBoardTypes.Clients,
                CreatedFromUtc = new DateTime(2026, 3, 10, 3, 0, 0, DateTimeKind.Utc),
                CreatedToUtcExclusive = new DateTime(2026, 3, 17, 3, 0, 0, DateTimeKind.Utc),
                TotalTelegramLeads = 12,
                ClientsLeads = 12
            });

        var controller = new TelegramInsightsController(kanbanServiceMock.Object);

        var result = controller.Index(new AdminTelegramBusinessDashboardFilterInputModel
        {
            BoardType = AdminKanbanBoardTypes.Clients,
            StartDate = new DateTime(2026, 3, 10),
            EndDate = new DateTime(2026, 3, 16)
        });

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AdminTelegramBusinessDashboardPageViewModel>(view.Model);
        Assert.Equal("Painel Telegram", model.Title);
        Assert.Equal(AdminKanbanBoardTypes.Clients, model.SelectedBoardType);
        Assert.Equal("Clientes", model.SelectedBoardLabel);
        Assert.Equal(12, model.Snapshot.TotalTelegramLeads);
        Assert.Equal("2026-03-10", model.PeriodStartValue);
        Assert.Equal("2026-03-16", model.PeriodEndValue);

        kanbanServiceMock.VerifyAll();
    }
}
