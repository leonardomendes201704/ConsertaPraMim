using ConsertaPraMim.Web.Admin.Controllers;
using ConsertaPraMim.Web.Admin.Models;
using ConsertaPraMim.Web.Admin.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Controllers;

public class AdminErDiagramControllerTests
{
    [Fact(DisplayName = "AdminErDiagram | Deve retornar a view com o schema atual do EF")]
    public async Task Index_ShouldReturnViewModelFromSchemaService()
    {
        var expectedViewModel = new AdminDatabaseSchemaViewModel
        {
            TotalTables = 1,
            TotalRelationships = 1,
            Tables =
            [
                new AdminDatabaseSchemaTableViewModel
                {
                    Schema = "dbo",
                    Name = "Users",
                    FullName = "dbo.Users",
                    DomainName = "Identidade",
                    TotalColumns = 2,
                    Columns =
                    [
                        new AdminDatabaseSchemaColumnViewModel
                        {
                            Name = "Id",
                            StoreType = "uniqueidentifier",
                            IsPrimaryKey = true
                        },
                        new AdminDatabaseSchemaColumnViewModel
                        {
                            Name = "RoleId",
                            StoreType = "uniqueidentifier",
                            IsForeignKey = true
                        }
                    ]
                }
            ],
            Relationships =
            [
                new AdminDatabaseSchemaRelationshipViewModel
                {
                    PrincipalTable = "dbo.Roles",
                    DependentTable = "dbo.Users",
                    PrincipalColumns = ["Id"],
                    DependentColumns = ["RoleId"],
                    IsRequired = true
                }
            ]
        };

        var schemaServiceMock = new Mock<IAdminDatabaseSchemaService>();
        schemaServiceMock
            .Setup(service => service.BuildViewModelAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedViewModel);

        var controller = new AdminErDiagramController(schemaServiceMock.Object);

        var result = await controller.Index(CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Same(expectedViewModel, viewResult.Model);
        schemaServiceMock.Verify(
            service => service.BuildViewModelAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
