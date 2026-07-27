using System.Security.Claims;
using BingeWatch.API.Dtos;
using BingeWatch.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BingeWatch.API.Controllers
{
    [ApiController]
    [Route("api/lists")]
    public class ListsController : ControllerBase
    {
        private readonly IUserListService _listService;

        public ListsController(IUserListService listService)
        {
            _listService = listService;
        }

        private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        /// <summary>Anonim istekte <c>null</c> — yalnızca herkese açık listeler görünür.</summary>
        private string? ViewerId => User.Identity?.IsAuthenticated == true
            ? User.FindFirstValue(ClaimTypes.NameIdentifier)
            : null;

        [HttpGet("{listId:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetDetail(int listId)
        {
            var list = await _listService.GetDetailAsync(listId, ViewerId);
            return list == null ? NotFound() : Ok(list);
        }

        /// <summary>"Listeye ekle" menüsü — kullanıcının listeleri + dizinin üyeliği.</summary>
        [HttpGet("membership")]
        [Authorize]
        public async Task<IActionResult> GetMembership([FromQuery] int tmdbShowId)
        {
            return Ok(await _listService.GetMembershipAsync(CurrentUserId, tmdbShowId));
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Create([FromBody] UpsertListRequest request)
        {
            var list = await _listService.CreateAsync(CurrentUserId, request);
            if (list == null)
                return BadRequest(new { message = "Liste başlığı boş olamaz." });

            return CreatedAtAction(nameof(GetDetail), new { listId = list.Id }, list);
        }

        [HttpPut("{listId:int}")]
        [Authorize]
        public async Task<IActionResult> Update(int listId, [FromBody] UpsertListRequest request)
        {
            var list = await _listService.UpdateAsync(CurrentUserId, listId, request);
            return list == null ? NotFound() : Ok(list);
        }

        [HttpDelete("{listId:int}")]
        [Authorize]
        public async Task<IActionResult> Delete(int listId)
        {
            var deleted = await _listService.DeleteAsync(CurrentUserId, listId);
            return deleted ? NoContent() : NotFound();
        }

        [HttpPost("{listId:int}/items")]
        [Authorize]
        public async Task<IActionResult> AddItem(int listId, [FromBody] AddListItemRequest request)
        {
            var item = await _listService.AddItemAsync(CurrentUserId, listId, request);
            return item == null ? NotFound() : Ok(item);
        }

        [HttpPut("{listId:int}/items/{itemId:int}")]
        [Authorize]
        public async Task<IActionResult> UpdateItem(int listId, int itemId,
            [FromBody] UpdateListItemRequest request)
        {
            var item = await _listService.UpdateItemAsync(CurrentUserId, listId, itemId, request);
            return item == null ? NotFound() : Ok(item);
        }

        [HttpDelete("{listId:int}/items/{itemId:int}")]
        [Authorize]
        public async Task<IActionResult> RemoveItem(int listId, int itemId)
        {
            var removed = await _listService.RemoveItemAsync(CurrentUserId, listId, itemId);
            return removed ? NoContent() : NotFound();
        }

        [HttpPut("{listId:int}/order")]
        [Authorize]
        public async Task<IActionResult> Reorder(int listId, [FromBody] ReorderListRequest request)
        {
            var list = await _listService.ReorderAsync(CurrentUserId, listId, request);
            return list == null ? NotFound() : Ok(list);
        }
    }
}
