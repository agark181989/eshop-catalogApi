using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CatalogAPI.Infrastructure;
using CatalogAPI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Microsoft.AspNetCore.Cors;
using System.Net;
using Microsoft.AspNetCore.Authorization;
using CatalogAPI.Helpers;
using Microsoft.Extensions.Configuration;

namespace CatalogAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    //[EnableCors("AllowPartners")]    
    public class CatalogController : ControllerBase
    {
        private CatalogContext db;
        private IConfiguration configuration;
        public CatalogController(CatalogContext db, IConfiguration configuration)
        {
            this.db = db;
            this.configuration = configuration;
        }

        [AllowAnonymous]
        [HttpGet("", Name ="GetProducts")]
        public async Task<ActionResult<List<CatalogItem>>> GetProducts()
        {

            var result = await this.db.Catalog.FindAsync<CatalogItem>(FilterDefinition<CatalogItem>.Empty);            
            return result.ToList();
        }

        [AllowAnonymous]
        [HttpGet("{id}", Name = "FindById")]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<ActionResult<CatalogItem>> FindProductById(string id)
        {
            var builder = Builders<CatalogItem>.Filter;
            var filter = builder.Eq("Id", id);
            var result = await db.Catalog.FindAsync(filter);
            var item = result.FirstOrDefault();
            if (item == null)
                return NotFound();            // not found status code 404
            else
                return Ok(item);             // success  status code 200


        }

        [Authorize(Roles ="admin")]
        [HttpPost("", Name ="AddProduct")]
        //To inform the swigger about status code so we need to add below lines.
        [ProducesResponseType((int)HttpStatusCode.Created)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        public ActionResult<CatalogItem> AddProduct(CatalogItem item)
        {
            TryValidateModel(item);
            if (ModelState.IsValid)
            {
                this.db.Catalog.InsertOne(item);
                return Created("", item); //status code 201
            }
            else
            {
                return BadRequest(ModelState); //status code 400
            }
        }
        
        [HttpPost("product")]
        public async Task<ActionResult<CatalogItem>> AddProduct()
        {
            //var imageName = SaveImageToLocal(Request.Form.Files[0]);
            var imageName = SaveImageToCloudAsync(Request.Form.Files[0]).GetAwaiter().GetResult();
            var catalogItem = new CatalogItem()
            {
                Name = Request.Form["name"],
                Price = Double.Parse(Request.Form["price"]),
                Quantity = Int32.Parse(Request.Form["quantity"]),
                ReorderLevel = Int32.Parse(Request.Form["reorderLevel"]),
                ManufacturingDate = DateTime.Parse(Request.Form["manufacturingDate"]),
                Vendors = new List<Vendor>(),
                ImageUrl = imageName
            };
            db.Catalog.InsertOne(catalogItem);
            //Backup to Azure table storage
            await BackupToTableAsync(catalogItem);
            return catalogItem;

        }

        [NonAction]
        private string SaveImageToLocal(IFormFile image)
        {
            var imageName = $"{Guid.NewGuid()}_{ Request.Form.Files[0].FileName}";
            //var image = Request.Form.Files[0];
            var dirName = Path.Combine(Directory.GetCurrentDirectory(), "Images");
            if (!Directory.Exists(dirName))
            {
                Directory.CreateDirectory(dirName);
            }
            var filePath = Path.Combine(dirName, imageName);
            using (FileStream fs = new FileStream(filePath, FileMode.Create))
            {
                image.CopyTo(fs);
            }
            return $"/Images/{imageName}";
        }

        [NonAction]
        private async Task<string> SaveImageToCloudAsync(IFormFile image)
        {
            var imageName = $"{Guid.NewGuid()}_{image.FileName}";
            var tempFile = Path.GetTempFileName();
            using (FileStream fs = new FileStream(tempFile, FileMode.Create))
            {
                await image.CopyToAsync(fs);
            }

            var imageFile = Path.Combine(Path.GetDirectoryName(tempFile), imageName);
            System.IO.File.Move(tempFile, imageFile);
            StorageAccountHelper storageHelper = new StorageAccountHelper();
            storageHelper.StorageConnectionString = configuration.GetConnectionString("StorageConnection");
            var fileUri = await storageHelper.UploadFileToBlobAsync(imageFile, "eshopimages");
            System.IO.File.Delete(imageFile);
             //+ "?sv=2019-02-02&ss=b&srt=co&sp=rl&se=2019-11-06T14:58:12Z&st=2019-11-06T06:58:12Z&spr=https&sig=5L1b%2Fn3Rfr5V7X5m4OwiLxnQ9OF6SDjuiFf9ax3rWvY%3D";
            return fileUri;
        }

        [NonAction]
        private async Task<CatalogEntity> BackupToTableAsync(CatalogItem item)
        {
            StorageAccountHelper storageHelper = new StorageAccountHelper();
            storageHelper.TableConnectionString = configuration.GetConnectionString("TableConnection");
            return await storageHelper.SaveToTableAsync(item);
        }
    }
}