using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace FileDownloadStreamProcessing.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FileDownloadController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<FileDownloadController> _logger;

        public FileDownloadController(IHttpClientFactory httpClientFactory, ILogger<FileDownloadController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        [HttpGet("download")]
        public async Task<IActionResult> DownloadFile(string fileUrl)
        {
            if (string.IsNullOrEmpty(fileUrl))
            {
                return BadRequest("Invalid file URL.");
            }

            try
            {
                var client = _httpClientFactory.CreateClient();
                var response = await client.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead);

                response.EnsureSuccessStatusCode();

                var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
                var fileName = fileUrl.Split('/').Last();
                var contentLength = response.Content.Headers.ContentLength;

                var rangeHeader = Request.Headers[HeaderNames.Range].ToString();
                var fileStream = await response.Content.ReadAsStreamAsync();

                if (!string.IsNullOrEmpty(rangeHeader))
                {
                    if (RangeHeaderValue.TryParse(rangeHeader, out var range))
                    {
                        var totalLength = contentLength ?? 0;
                        var from = range.Ranges.First().From ?? 0;
                        var to = range.Ranges.First().To ?? totalLength - 1;

                        if (from < 0 || to >= totalLength || from > to)
                        {
                            return StatusCode((int)HttpStatusCode.RequestedRangeNotSatisfiable);
                        }

                        // Create a stream to serve the partial content
                        var partialStream = new PartialContentStream(fileStream, from, to);

                        Response.Headers[HeaderNames.ContentRange] = new ContentRangeHeaderValue(from, to, totalLength).ToString();
                        Response.ContentLength = to - from + 1;
                        Response.StatusCode = (int)HttpStatusCode.PartialContent;

                        return File(partialStream, contentType, fileName);
                    }

                    return BadRequest("Invalid Range header.");
                }

                return File(fileStream, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return StatusCode(500, "Internal server error.");
            }
        }
    }

    public class PartialContentStream : Stream
    {
        private readonly Stream _baseStream;
        private readonly long _start;
        private readonly long _end;
        private long _position;

        public PartialContentStream(Stream baseStream, long start, long end)
        {
            _baseStream = baseStream;
            _start = start;
            _end = end;
            _position = start;
        }

        public override bool CanRead => _baseStream.CanRead;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => _end - _start + 1;

        public override long Position
        {
            get => _position - _start;
            set => Seek(value + _start, SeekOrigin.Begin);
        }

        public override void Flush() => _baseStream.Flush();

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_position >= _end)
            {
                return 0;
            }

            var readCount = (int)Math.Min(count, _end - _position + 1);
            var bytesRead = _baseStream.Read(buffer, offset, readCount);
            _position += bytesRead;
            return bytesRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            var newPosition = origin switch
            {
                SeekOrigin.Begin => _start + offset,
                SeekOrigin.Current => _position + offset,
                SeekOrigin.End => _end + offset,
                _ => throw new ArgumentOutOfRangeException(nameof(origin))
            };

            if (_position < _start || _position > _end)
            {
                throw new ArgumentOutOfRangeException();
            }

            _position = newPosition;
            return newPosition;
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
    }
}
