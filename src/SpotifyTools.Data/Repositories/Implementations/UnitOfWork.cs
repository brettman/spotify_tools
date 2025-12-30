using Microsoft.EntityFrameworkCore.Storage;
using SpotifyTools.Data.Repositories.Interfaces;
using SpotifyTools.Domain.Entities;

namespace SpotifyTools.Data.Repositories.Implementations;

/// <summary>
/// Unit of Work implementation - coordinates repositories and transactions
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly DbContext.SpotifyDbContext _context;
    private IDbContextTransaction? _transaction;

    // Repository instances
    private ITrackRepository? _tracks;
    private IRepository<Artist>? _artists;
    private IRepository<Album>? _albums;
    private IRepository<AudioFeatures>? _audioFeatures;
    private IRepository<Playlist>? _playlists;
    private IRepository<SpotifyToken>? _spotifyTokens;
    private IRepository<SyncHistory>? _syncHistory;

    public UnitOfWork(DbContext.SpotifyDbContext context)
    {
        _context = context;
    }

    public ITrackRepository Tracks => _tracks ??= new TrackRepository(_context);
    public IRepository<Artist> Artists => _artists ??= new Repository<Artist>(_context);
    public IRepository<Album> Albums => _albums ??= new Repository<Album>(_context);
    public IRepository<AudioFeatures> AudioFeatures => _audioFeatures ??= new Repository<AudioFeatures>(_context);
    public IRepository<Playlist> Playlists => _playlists ??= new Repository<Playlist>(_context);
    public IRepository<SpotifyToken> SpotifyTokens => _spotifyTokens ??= new Repository<SpotifyToken>(_context);
    public IRepository<SyncHistory> SyncHistory => _syncHistory ??= new Repository<SyncHistory>(_context);

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction == null)
        {
            throw new InvalidOperationException("No transaction has been started.");
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await _transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await RollbackTransactionAsync();
            throw;
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync()
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync();
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }
}
