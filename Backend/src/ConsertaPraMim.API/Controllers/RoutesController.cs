using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.API.Controllers;

[Authorize]
[ApiController]
[Route("api/routes")]
public class RoutesController : ControllerBase
{
    private readonly IDrivingRouteService _drivingRouteService;

    public RoutesController(IDrivingRouteService drivingRouteService)
    {
        _drivingRouteService = drivingRouteService;
    }

    [HttpGet("driving")]
    public async Task<IActionResult> GetDrivingRoute(
        [FromQuery] double providerLat,
        [FromQuery] double providerLng,
        [FromQuery] double requestLat,
        [FromQuery] double requestLng,
        CancellationToken cancellationToken)
    {
        if (!AreValidCoordinates(providerLat, providerLng) || !AreValidCoordinates(requestLat, requestLng))
        {
            return BadRequest(new { success = false, message = "Coordenadas invalidas para calculo de rota." });
        }

        var route = await _drivingRouteService.GetDrivingRouteAsync(
            providerLat,
            providerLng,
            requestLat,
            requestLng,
            cancellationToken);

        if (!route.Success)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                success = false,
                message = route.ErrorMessage ?? "Nao foi possivel calcular rota de carro no momento."
            });
        }

        return Ok(new
        {
            success = true,
            distance = route.DistanceMeters,
            duration = route.DurationSeconds,
            geometry = route.Geometry
        });
    }

    private static bool AreValidCoordinates(double latitude, double longitude)
    {
        return latitude is >= -90 and <= 90 &&
               longitude is >= -180 and <= 180;
    }
}
