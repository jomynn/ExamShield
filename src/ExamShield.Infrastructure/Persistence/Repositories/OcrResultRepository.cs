using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ExamShield.Infrastructure.Persistence.Repositories;

public sealed class OcrResultRepository : IOcrResultRepository
{
    private readonly ExamShieldDbContext _context;

    public OcrResultRepository(ExamShieldDbContext context) => _context = context;

    public async Task AddAsync(OcrResult result, CancellationToken ct = default)
    {
        await _context.OcrResults.AddAsync(result, ct);
        await _context.SaveChangesAsync(ct);
    }

    public Task<OcrResult?> GetByCaptureIdAsync(CaptureId captureId, CancellationToken ct = default) =>
        _context.OcrResults.FirstOrDefaultAsync(r => r.CaptureId == captureId, ct);

    public Task<OcrResult?> GetByIdAsync(OcrResultId id, CancellationToken ct = default) =>
        _context.OcrResults.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<OcrResult>> ListCompletedAsync(CancellationToken ct = default) =>
        await _context.OcrResults.Where(r => r.Status == OcrStatus.Completed).ToListAsync(ct);

    public async Task<IReadOnlyList<OcrResult>> ListByCaptureIdsAsync(
        IReadOnlyList<CaptureId> captureIds, CancellationToken ct = default)
    {
        var ids = new HashSet<Guid>(captureIds.Select(c => c.Value));
        return await _context.OcrResults.Where(o => ids.Contains(o.CaptureId.Value)).ToListAsync(ct);
    }
}
