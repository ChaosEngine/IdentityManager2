using InkBall.Module.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace InkBall.Module.Hubs
{
	public interface IGameClient
	{
		Task ServerToClientPoint(InkBallPointViewModel point);

		Task ServerToClientPath(InkBallPathViewModel path);

		Task ServerToClientPing(PingCommand ping);

		Task ServerToClientPlayerJoin(PlayerJoiningCommand join);

		Task ServerToClientPlayerSurrender(PlayerSurrenderingCommand surrender);

		Task ServerToClientPlayerWin(WinCommand win);

		Task ServerToClientStopAndDraw(StopAndDrawCommand notification);

		Task ServerToClientOtherPlayerDisconnected(string message);

		Task ServerToClientOtherPlayerConnected(string message);
	}

	public interface IGameServer
	{
		Task<DateTime?> ClientToServerPoint(InkBallPointViewModel point);

		Task<CpuMoveBatchResponse> ClientToServerPointWithCpuMove(CpuMoveBatchRequest request);

		Task<IDtoMsg> ClientToServerPath(InkBallPathViewModel path);

		// Task<IDtoMsg> ClientToServerCheck4Win();

		Task ClientToServerPing(PingCommand ping);

		Task<PlayerPointsAndPathsDTO> GetPlayerPointsAndPaths(bool viewOnly, int gameID);

		Task<string> GetUserSettings();

		Task ClientToServerStopAndDraw(StopAndDrawCommand notification);
	}

	[Authorize(Policy = Constants.InkBallPolicyName)]
	public class GameHub : Hub<IGameClient>, IGameServer
	{
		#region Fields

		public const string HubName = "gameHub";

		//Allowed origins here
		internal static readonly SynchronizedCache<string> WebSocketAllowedOrigins = new SynchronizedCache<string>();

		private static readonly SimpleCoordsPointComparer _simpleCoordsPointComparer = new SimpleCoordsPointComparer();

		private static readonly InkBallPoint.StatusEnum[] _inPathColors = [InkBallPoint.StatusEnum.POINT_IN_PATH, InkBallPoint.StatusEnum.POINT_STARTING];

		// In-memory cache of completed paths per game; paths are immutable once created
		internal static readonly ConcurrentDictionary<int, List<InkBallPath>> _pathsCache = new();

		private readonly GamesContext _dbContext;
		private readonly ILogger<GameHub> _logger;

		#endregion Fields

		#region Properties

		public InkBallPlayer ThisPlayer { get; private set; }

		public InkBallPlayer OtherPlayer { get; private set; }

		public int? ThisGameID
		{
			get
			{
				if (base.Context.Items.TryGetValue(nameof(ThisGameID), out object oObj) && oObj is int iVal)
					return iVal;
				return null;
			}
			private set
			{
				base.Context.Items[nameof(ThisGameID)] = value.GetValueOrDefault(0);
			}
		}

		public int? ThisPlayerID
		{
			get
			{
				if (base.Context.Items.TryGetValue(nameof(ThisPlayerID), out object oObj) && oObj is int iVal)
					return iVal;
				return null;
			}
			private set
			{
				base.Context.Items[nameof(ThisPlayerID)] = value.GetValueOrDefault(0);
			}
		}

		public InkBallGame ThisGame { get; private set; }

		internal string ThisUserIdentifier => base.Context.UserIdentifier;

		public string OtherUserIdentifier { get; private set; }

		public string ThisUserName { get; private set; }

		#endregion Properties

		#region Private methods

		private void ValidateOriginHeaderAndAccessToken(HttpContext ctx)
		{
			//Game page populates WebSocketAllowedOrigins
			if (WebSocketAllowedOrigins.Any() && ctx.Request.Headers.TryGetValue(HeaderNames.Upgrade, out var upgrade))
			{
				if (string.Equals(upgrade, "websocket", StringComparison.InvariantCultureIgnoreCase))
				{
					if (ctx.Request.Host.HasValue && !WebSocketAllowedOrigins.ContainsValue(ctx.Request.Host.Host))
					{
						throw new UnauthorizedAccessException("Origin not allowed!");
					}
				}
			}
		}

		private (int gameID, int playerID) GetGameIdAndPlayerIDFromContext(HttpContext ctx)
		{
			//Authorization: Bearer iGameID=123 or Query: iGameID=123&iPlayerID=456
			if (!(ctx.Request.Headers.TryGetValue(HeaderNames.Authorization, out var access_token)
				|| ctx.Request.Query.TryGetValue("access_token", out access_token))
				|| string.IsNullOrEmpty(access_token))
			{
				throw new UnauthorizedAccessException($"{nameof(access_token)} not passed!");
			}

			var token_vars = GetGameIDFromAccessToken();
			return token_vars;

			//
			// private internal function
			//
			(int gameID, int playerID) GetGameIDFromAccessToken()
			{
				string value = access_token.FirstOrDefault();
				if (value.StartsWith("bearer ", StringComparison.InvariantCultureIgnoreCase))
					value = value.Substring("bearer ".Length);

				var queryDictionary = QueryHelpers.ParseQuery(value);

				if (!queryDictionary.TryGetValue("iGameID", out StringValues str) || !int.TryParse(str, out int game_id))
					game_id = 0;

				if (!queryDictionary.TryGetValue("iPlayerID", out str) || !int.TryParse(str, out int player_id))
					player_id = 0;

				return (game_id, player_id);
			}
		}

		private async Task LoadGameAndPlayerStructures(CancellationToken token)
		{
			var claimsPrincipal = this.Context.User;
			var thisUserIdentifier = ThisUserIdentifier;

			if (ThisGame == null || ThisPlayer == null)
			{
				(InkBallGame game, InkBallPlayer this_Player) = await GetGameAndThisPlayer(claimsPrincipal, thisUserIdentifier,
					ThisGameID.Value, ThisPlayerID.Value, token);
				ThisGame = game;
				ThisPlayer = this_Player;
			}

			if (ThisGame == null || ThisPlayer == null) return;

			// bIsPlayer1 is connection-specific transient state; restore it on each call
			// because ThisGame entity can be shared/tracked across hubs in tests/runtime.
			ThisGame.bIsPlayer1 = ThisGame.iPlayer1Id == ThisPlayer.iId;

			ThisUserName = ThisPlayer.UserName;

			if (OtherPlayer == null || OtherUserIdentifier == null)
			{
				(InkBallPlayer other_Player, string other_UserIdentifier) = GetOtherPlayer(ThisGame, thisUserIdentifier, ThisPlayer);
				OtherPlayer = other_Player;
				OtherUserIdentifier = other_UserIdentifier;
			}
		}

		private (InkBallPlayer other_Player, string other_UserIdentifier) GetOtherPlayer(InkBallGame game,
			string thisUserIdentifier, IPlayer<InkBallPoint, InkBallPath> this_Player)
		{
			InkBallPlayer other_Player;
			string other_UserIdentifier;
			//if (
			//	!(this.Context.Items.TryGetValue(nameof(OtherPlayer), out var opObj) && opObj is IPlayer<InkBallPointViewModel, InkBallPathViewModel> other_Player)
			//	|| !(this.Context.Items.TryGetValue(nameof(OtherUserIdentifier), out var ouidenObj) && ouidenObj is string other_UserIdentifier)
			//	)
			{
				//obtain another player; co-player
				InkBallPlayer otherDbPlayer = game.Player1.sExternalId == thisUserIdentifier ? game.Player2 : game.Player1;
				if (otherDbPlayer != null)
				{
					// For non-CPU players, verify they have different external IDs
					if (otherDbPlayer.iId == this_Player.iId ||
						(!string.IsNullOrEmpty(otherDbPlayer.sExternalId) && otherDbPlayer.sExternalId == thisUserIdentifier))
					{
						throw new ArgumentException("both game players are the same");
					}

					other_Player = otherDbPlayer;
					other_UserIdentifier = otherDbPlayer.sExternalId;

					//Check for other player being CPU player which is treated specially.
					if (string.IsNullOrEmpty(other_UserIdentifier) && otherDbPlayer.iId == -1)
					{
						other_UserIdentifier = thisUserIdentifier;
					}
				}
				else
				{
					other_Player = null;
					other_UserIdentifier = null;
				}
				//this.Context.Items[nameof(OtherPlayer)] = other_Player;
				//this.Context.Items[nameof(OtherUserIdentifier)] = other_UserIdentifier;
			}

			return (other_Player, other_UserIdentifier);
		}

		private async Task<(InkBallGame, InkBallPlayer)> GetGameAndThisPlayer(ClaimsPrincipal claimsPrincipal, string thisUserIdentifier, int gameID, int playerID, CancellationToken token)
		{
			InkBallGame game;
			InkBallPlayer this_Player;
			//if (!(this.Context.Items.TryGetValue(nameof(ThisGame), out object gObj) && gObj is InkBallGameViewModel game))
			{
				//get game from db
				InkBallGame dbGame = await _dbContext.InkBallGame
				.Include(gp1 => gp1.Player1)
				.Include(gp2 => gp2.Player2)
				.FirstOrDefaultAsync(
					w => w.iId == gameID
					&& (w.iPlayer1Id == playerID || w.iPlayer2Id == playerID)
					&& (w.Player1.sExternalId == thisUserIdentifier || w.Player2.sExternalId == thisUserIdentifier)
					&& (GamesContext.ActiveVisibleGameStates.Contains(w.GameState))
				, token);
				if (dbGame == null)
				{
					// throw new NoGameArgumentNullException(nameof(dbGame), "game == null");
					return (null, null);
				}
				if (!(dbGame.iPlayer1Id == ThisPlayerID || (dbGame.iPlayer2Id.HasValue && dbGame.iPlayer2Id.Value == ThisPlayerID)))
				{
					// throw new ArgumentException("no player exist in that game");
					return (dbGame, null);
				}

				bool bIsPlayer1;
				if (thisUserIdentifier == dbGame.Player1.sExternalId)
				{
					this_Player = dbGame.Player1;
					bIsPlayer1 = true;
				}
				else if (thisUserIdentifier == dbGame.Player2.sExternalId)
				{
					this_Player = dbGame.Player2;
					bIsPlayer1 = false;
				}
				else
				{
					// throw new ArgumentNullException(nameof(thisUserIdentifier), "player not found");
					return (dbGame, null);
				}
				dbGame.bIsPlayer1 = bIsPlayer1;

				//additional validation
				string value = claimsPrincipal.FindFirstValue(nameof(Pages.BasePageModel.InkBalPlayerId));
				int.TryParse(value, out int this_PlayerId);
				if (this_PlayerId != this_Player.iId)
				{
					// throw new ArgumentException("this_PlayerId != this_Player.iId", nameof(this_Player.iId));
					return (dbGame, null);
				}

				game = dbGame;
				//this.Context.Items[nameof(ThisGame)] = game;
				//this.Context.Items[nameof(ThisPlayer)] = this_Player;
			}
			return (game, this_Player);
		}

		private async Task<InkBallPlayer> GetPlayer(ClaimsPrincipal claimsPrincipal, string thisUserIdentifier,
			CancellationToken token)
		{
			InkBallPlayer this_Player;
			//if (!(this.Context.Items.TryGetValue(nameof(ThisPlayer), out object pobj) && pobj is IPlayer<InkBallPointViewModel, InkBallPathViewModel> this_Player))
			{
				//get player from db
				InkBallPlayer dbPlayer = await _dbContext.InkBallPlayer
					.FirstOrDefaultAsync(p => p.sExternalId == thisUserIdentifier, token);
				if (dbPlayer == null)
					throw new ArgumentNullException(nameof(dbPlayer), "no player");

				//additional validation
				string value = claimsPrincipal.FindFirstValue(nameof(Pages.BasePageModel.InkBalPlayerId));
				int.TryParse(value, out int this_UserId);
				if (this_UserId != dbPlayer.iId)
					throw new ArgumentException("this_UserId != dbPlayer.iId", nameof(dbPlayer.iId));

				this_Player = dbPlayer;
				//this.Context.Items[nameof(ThisPlayer)] = this_Player;
			}
			return this_Player;
		}

		private async Task OtherPlayerConnectNotification(CancellationToken token)
		{
			try
			{
				await LoadGameAndPlayerStructures(token);

				if (ThisGame == null || ThisPlayer == null || OtherPlayer == null || string.IsNullOrEmpty(OtherUserIdentifier)
					|| string.IsNullOrEmpty(ThisUserName) || ThisGame.CpuOponent == true)
					return;

				var msg = $"Other player {ThisPlayer?.UserName} connected 😁;{ThisPlayer?.UserName}";
				await Clients.User(OtherUserIdentifier).ServerToClientOtherPlayerConnected(msg);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex.Message);
			}
		}

		private async Task OtherPlayerDisconnectNotification(Exception exception, CancellationToken token = default)
		{
			if (exception != null)
				_logger.LogInformation(exception, nameof(OnDisconnectedAsync));

			try
			{
				await LoadGameAndPlayerStructures(token);

				if (ThisGame == null || ThisPlayer == null || OtherPlayer == null || string.IsNullOrEmpty(OtherUserIdentifier)
					|| string.IsNullOrEmpty(ThisUserName) || ThisGame.CpuOponent == true)
					return;

				var msg = $"Other player {ThisPlayer?.UserName} disconnected 😢;{ThisPlayer?.UserName}";
				await Clients.User(OtherUserIdentifier).ServerToClientOtherPlayerDisconnected(msg);
			}
			//catch (NoGameArgumentNullException ex)
			//{
			//	_logger.LogWarning(ex, ex.Message);
			//}
			catch (Exception ex)
			{
				_logger.LogError(ex.Message);
			}
		}


		/// <summary>
		/// Returns paths for the given game using an in-memory cache per gameId.
		/// Paths are immutable once placed, so only paths newer than what is already
		/// cached are fetched from the database; the rest come from the local cache.
		/// </summary>
		private async Task<IEnumerable<InkBallPath>> GetPathsWithCacheAsync(int gameId, CancellationToken token)
		{
			_pathsCache.TryGetValue(gameId, out var cachedPaths);

			int maxCachedId = cachedPaths?.Count > 0 ? cachedPaths.Max(p => p.iId) : 0;

			var newPaths = (await _dbContext.GetPathsFromDatabaseAsync(gameId, false, true, maxCachedId, token: token)).ToList();

			if (newPaths.Count == 0)
				return cachedPaths ?? [];

			var allPaths = cachedPaths != null ? [.. cachedPaths, .. newPaths] : newPaths;

			_pathsCache[gameId] = allPaths;
			return allPaths;
		}

		#endregion Private methods

		public GameHub(GamesContext dbContext, ILogger<GameHub> logger)
		{
			_dbContext = dbContext;
			_logger = logger;
		}

		public override async Task OnConnectedAsync()
		{
			var ctx = this.Context.GetHttpContext();

			ValidateOriginHeaderAndAccessToken(ctx);

			(int gameID, int playerID) = GetGameIdAndPlayerIDFromContext(ctx);
			ThisGameID = gameID;
			ThisPlayerID = playerID;


			await OtherPlayerConnectNotification(this.Context.ConnectionAborted);
		}

		public override async Task OnDisconnectedAsync(Exception exception)
		{
			//Can not pass Context.ConnectionAborted because it would stop from sending disconnect notification to other player
			await OtherPlayerDisconnectNotification(exception/*, Context.ConnectionAborted*/);
		}

		#region IGameServer implementation

		public async Task<DateTime?> ClientToServerPoint(InkBallPointViewModel point)
		{
			CancellationToken token = this.Context.ConnectionAborted;

			await LoadGameAndPlayerStructures(token);

			try
			{
				if (ThisGame == null || ThisPlayer == null || OtherPlayer == null || string.IsNullOrEmpty(OtherUserIdentifier)
					|| string.IsNullOrEmpty(ThisUserName))
				{
					throw new NoGameArgumentNullException("bad player or game");
				}
				if (!ThisGame.IsThisPlayerActive(point))
				{
					throw new ArgumentException("not your turn");
				}

				var current_player_color = ThisGame.IsThisPlayerPlayingWithRed(point) ? InkBallPoint.StatusEnum.POINT_FREE_RED : InkBallPoint.StatusEnum.POINT_FREE_BLUE;

				if (point == null || point.iGameId != ThisGame.iId || point.Status != current_player_color
					|| point.iX < 0 || point.iY < 0 || point.iX > ThisGame.iBoardWidth || point.iY > this.ThisGame.iBoardHeight)
					throw new ArgumentException("bad point");
				if (point.iPlayerId != ThisPlayer.iId && point.iPlayerId != OtherPlayer.iId)
					throw new ArgumentException("bad Player ID");


				var already_placed = _dbContext.InkBallPoint.Local.Any(pts =>
									pts.iGameId == ThisGame.iId
									&& pts.iX == point.iX && pts.iY == point.iY)
					|| await _dbContext.InkBallPoint.AsNoTracking().AnyAsync(pts =>
									pts.iGameId == ThisGame.iId
									&& pts.iX == point.iX && pts.iY == point.iY,
									token);
				var db_point_player = ThisPlayer.iId == point.iPlayerId ? ThisPlayer : OtherPlayer;
				if (already_placed)
					throw new ArgumentException($"point already placed ({point})");

				var game_paths = await GetPathsWithCacheAsync(point.iGameId, token);
				if (game_paths.Any(pa => pa.IsPointInsidePath(point)))
					throw new ArgumentException("point inside path");

				var db_point = new InkBallPoint
				{
					//iId = point.iId,
					iGameId = ThisGame.iId,
					iPlayerId = point.iPlayerId,
					iX = point.iX,
					iY = point.iY,
					Status = point.Status,
					iEnclosingPathId = point.iEnclosingPathId <= 0 ? null : point.iEnclosingPathId
				};
				InkBallPointViewModel new_point;

				var currentTransaction = _dbContext.Database.CurrentTransaction;
				var ownsTransaction = currentTransaction == null;
				if (ownsTransaction)
					currentTransaction = await _dbContext.Database.BeginTransactionAsync(token);

				try
				{
					await _dbContext.InkBallPoint.AddAsync(db_point, token);
					ThisGame.bIsPlayer1Active = !ThisGame.bIsPlayer1Active;

					new_point = new InkBallPointViewModel(db_point);
					if (!db_point_player.IsCpuPlayer)
					{
						string last_move = new_point.SerializeThin();
						db_point_player.sLastMoveCode = last_move;
					}

					//#if DEBUG
					//						throw new Exception($"FAKE EXCEPTION {new_point}");
					//#endif
					await _dbContext.SaveChangesAsync(token);

					if (ownsTransaction)
						await currentTransaction.CommitAsync(token);
				}
				catch (Exception ex)
				{
					if (ownsTransaction)
						await currentTransaction.RollbackAsync(token);
					_logger.LogError(ex, nameof(ClientToServerPoint));
					throw;
				}
				finally
				{
					if (ownsTransaction)
						await currentTransaction.DisposeAsync();
				}
				new_point.TimeStamp = ThisGame.TimeStamp.ToUniversalTime();

				if (!ThisGame.CpuOponent)
					await Clients.User(OtherUserIdentifier).ServerToClientPoint(new_point);

				return /* new_point */new_point.TimeStamp;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex.Message);
				throw;
			}
		}

		public async Task<IDtoMsg> ClientToServerPath(InkBallPathViewModel path)
		{
			CancellationToken token = this.Context.ConnectionAborted;
			await LoadGameAndPlayerStructures(token);

			try
			{
				if (ThisGame == null || ThisPlayer == null || OtherPlayer == null || string.IsNullOrEmpty(OtherUserIdentifier)
					|| string.IsNullOrEmpty(ThisUserName))
					throw new NoGameArgumentNullException("bad game or player");
				bool isDelayedPathDrawn = false;
				if (!ThisGame.IsThisPlayerActive(path)
					&& !(isDelayedPathDrawn = ThisPlayer.IsDelayedPathDrawPossible()))
					throw new ArgumentException("not your turn");

				if (path == null ||/*  path.iPlayerId <= 0 || */ path.iGameId <= 0 || path.iGameId != ThisGame.iId)
					throw new ArgumentException("bad path");
				if (path.iPlayerId != ThisPlayer.iId && path.iPlayerId != OtherPlayer.iId)
					throw new ArgumentException("bad Player ID");
				ICollection<InkBallPointViewModel> points_on_path = path.InkBallPoints;//serialize points from path int objects

				InkBallPoint.StatusEnum current_player_color, other_player_color, owning_color, other_owning_color;
				if (ThisGame.IsThisPlayerPlayingWithRed(path))
				{
					current_player_color = InkBallPoint.StatusEnum.POINT_FREE_RED;
					other_player_color = InkBallPoint.StatusEnum.POINT_FREE_BLUE;
					owning_color = InkBallPoint.StatusEnum.POINT_OWNED_BY_RED;
					other_owning_color = InkBallPoint.StatusEnum.POINT_OWNED_BY_BLUE;
				}
				else
				{
					current_player_color = InkBallPoint.StatusEnum.POINT_FREE_BLUE;
					other_player_color = InkBallPoint.StatusEnum.POINT_FREE_RED;
					owning_color = InkBallPoint.StatusEnum.POINT_OWNED_BY_BLUE;
					other_owning_color = InkBallPoint.StatusEnum.POINT_OWNED_BY_RED;
				}
				var db_path_player = ThisPlayer.iId == path.iPlayerId ? ThisPlayer : OtherPlayer;
				var other_player_db = db_path_player.IsCpuPlayer ? ThisPlayer : OtherPlayer;
				var all_placed_points_fromDB = await (from p in _dbContext.InkBallPoint
													  where p.iGameId == ThisGame.iId &&
													  (
														  (p.iEnclosingPathId == null && new[] { current_player_color, other_player_color }.Contains(p.Status)) ||
														  (p.iEnclosingPathId != null && _inPathColors.Contains(p.Status))
													  )
													  select p).Cast<IPoint>()
													  .ToDictionaryAsync(pip => pip, _simpleCoordsPointComparer, token);

				var db_path = new InkBallPath
				{
					// iId = path.iId,
					iGameId = path.iGameId,
					iPlayerId = path.iPlayerId,
					//PointsAsString = path.PointsAsString
				};

				var status = InkBallPoint.StatusEnum.POINT_STARTING;
				var last_point_in_path = points_on_path.Last();
				foreach (var pop in points_on_path)
				{
					//TODO: check in-path-next-point from start to end with closing
					if (!(all_placed_points_fromDB.TryGetValue(pop, out IPoint iobj) && iobj is InkBallPoint found)
						|| !(found.iPlayerId == db_path_player.iId &&
							(
								(found.iEnclosingPathId == null && (found.Status == current_player_color || _simpleCoordsPointComparer.Equals(found, last_point_in_path))) ||
								(found.iEnclosingPathId != null && _inPathColors.Contains(found.Status))
							))
						)
					{
						throw new ArgumentOutOfRangeException($"point not in path [{pop}]");
					}

					found.Status = status;
					status = InkBallPoint.StatusEnum.POINT_IN_PATH;
					found.EnclosingPath = db_path;
				}

				if (!isDelayedPathDrawn)
					ThisGame.bIsPlayer1Active = !ThisGame.bIsPlayer1Active;

				var owning_points = path.GetOwnedPoints(owning_color, other_player_db.iId);
				foreach (var op in owning_points)
				{
					if (!(all_placed_points_fromDB.TryGetValue(op, out IPoint iobj) && iobj is InkBallPoint found)
						|| !(found.Status == other_player_color && found.iPlayerId == other_player_db.iId))
					{
						throw new ArgumentOutOfRangeException($"owning point not found [{op}]");
					}
					if (!path.IsPointInsidePath(op))
					{
						throw new ArgumentOutOfRangeException($"owning point not found [{op}]");
					}

					found.Status = owning_color;
					found.EnclosingPath = db_path;
				}
				string last_move = path.SerializeThin();
				db_path.PointsAsString = last_move;
				if (!db_path_player.IsCpuPlayer)
					db_path_player.sLastMoveCode = last_move;

				var currentTransaction = _dbContext.Database.CurrentTransaction;
				var ownsTransaction = currentTransaction == null;
				if (ownsTransaction)
					currentTransaction = await _dbContext.Database.BeginTransactionAsync(token);

				try
				{
					await _dbContext.InkBallPath.AddAsync(db_path, token);

					await _dbContext.SaveChangesAsync(token);
					// #if DEBUG
					// 						var saved_pts = await _dbContext.LoadPointsAndPathsAsync(ThisGameID.Value, token);
					// 						var restored_from_db = saved_pts.Paths.LastOrDefault();
					// 						var str = InkBallPath.GetPathsAsJavaScriptArrayForPage(saved_pts.Paths);
					// 						throw new Exception($"FAKE EXCEPTION org pts:[{path.PointsAsString}], restored pts:[{str}], owned:[{path.OwnedPointsAsString}]");
					// #endif

					var statisticalPointAndPathCounter = new StatisticalPointAndPathCounter(_dbContext, ThisGame.iId,
						ThisPlayer.iId, OtherPlayer.iId, ref owning_color, ref other_owning_color, ref token);

					IDtoMsg dto;
					var win_status = await ThisGame.Check4Win(statisticalPointAndPathCounter, path);
					if (win_status != InkBallGame.WinStatusEnum.NO_WIN)
					{
						int? winningPlayerID = await _dbContext.HandleWinStatusAsync(win_status, ThisGame, token);

						var win = new WinCommand(win_status, winningPlayerID.GetValueOrDefault(0),
							$"Bravo {(win_status == InkBallGame.WinStatusEnum.GREEN_WINS ? "green" : "red")}!");
						path.TimeStamp = ThisGame.TimeStamp.ToUniversalTime();
						win.Path = path;

						dto = win;
						await Clients.User(OtherUserIdentifier).ServerToClientPlayerWin(win);
						_pathsCache.TryRemove(path.iGameId, out _);
					}
					else
					{
						path = new InkBallPathViewModel(db_path, path.PointsAsString, path.OwnedPointsAsString);
						path.TimeStamp = ThisGame.TimeStamp.ToUniversalTime();
						dto = path;
						if (!ThisGame.CpuOponent)
							await Clients.User(OtherUserIdentifier).ServerToClientPath(path);
					}

					if (ownsTransaction)
						await currentTransaction.CommitAsync(token);

					return dto;
				}
				catch (Exception ex)
				{
					if (ownsTransaction)
						await currentTransaction.RollbackAsync(token);
					_logger.LogError(ex, nameof(ClientToServerPath));
					throw;
				}
				finally
				{
					if (ownsTransaction)
						await currentTransaction.DisposeAsync();
				}
				//trans end
			}
			catch (Exception ex)
			{
				_logger.LogError(ex.Message);
				throw;
			}
		}//method end

		public async Task<CpuMoveBatchResponse> ClientToServerPointWithCpuMove(CpuMoveBatchRequest request)
		{
			CancellationToken token = this.Context.ConnectionAborted;

			await LoadGameAndPlayerStructures(token);

			if (ThisGame == null || ThisPlayer == null || OtherPlayer == null || string.IsNullOrEmpty(OtherUserIdentifier)
				|| string.IsNullOrEmpty(ThisUserName))
				throw new NoGameArgumentNullException("bad game or player");

			if (!ThisGame.CpuOponent)
				throw new InvalidOperationException("CPU move batching is allowed only for CPU games");

			if (request == null || request.HumanPoint == null)
				throw new ArgumentNullException(nameof(request), "request.HumanPoint == null");

			if (request.HumanPoint.iPlayerId != ThisPlayer.iId)
				throw new ArgumentException("human point must belong to current player", nameof(request.HumanPoint));

			if (request.CpuPoint != null && request.CpuPath != null)
				throw new ArgumentException("Only one CPU move (point or path) is allowed");

			if (request.CpuPoint != null && request.CpuPoint.iPlayerId != -1)
				throw new ArgumentException("cpu point must belong to CPU player", nameof(request.CpuPoint));

			if (request.CpuPath != null && request.CpuPath.iPlayerId != -1)
				throw new ArgumentException("cpu path must belong to CPU player", nameof(request.CpuPath));

			var ownsTransaction = _dbContext.Database.CurrentTransaction == null;
			var transaction = _dbContext.Database.CurrentTransaction;
			if (ownsTransaction)
				transaction = await _dbContext.Database.BeginTransactionAsync(token);

			var response = new CpuMoveBatchResponse();
			try
			{
				response.HumanPointTimeStamp = await ClientToServerPoint(request.HumanPoint);

				try
				{
					if (request.CpuPoint != null)
					{
						var cpuTs = await ClientToServerPoint(request.CpuPoint);
						response.CpuPoint = request.CpuPoint;
						response.CpuPoint.TimeStamp = cpuTs;
					}
					else if (request.CpuPath != null)
					{
						var dto = await ClientToServerPath(request.CpuPath);
						if (dto is InkBallPathViewModel path)
						{
							response.CpuPath = path;
						}
						else if (dto is WinCommand win)
						{
							response.CpuWin = win;
							response.CpuPath = win.Path;
						}
						else
						{
							response.CpuMoveError = "Unsupported CPU move response kind";
						}
					}
				}
				catch (Exception ex)
				{
					response.CpuMoveError = ex.Message;
					_logger.LogWarning(ex, nameof(ClientToServerPointWithCpuMove));
				}

				if (ownsTransaction)
					await transaction.CommitAsync(token);
			}
			catch (Exception ex)
			{
				if (ownsTransaction)
					await transaction.RollbackAsync(token);
				_logger.LogError(ex, nameof(ClientToServerPointWithCpuMove));
				throw;
			}
			finally
			{
				if (ownsTransaction)
					await transaction.DisposeAsync();
			}

			return response;
		}

		public async Task ClientToServerPing(PingCommand ping)
		{
			CancellationToken token = this.Context.ConnectionAborted;

			await LoadGameAndPlayerStructures(token);

			if (ThisGame == null || ThisPlayer == null || OtherPlayer == null || string.IsNullOrEmpty(OtherUserIdentifier)
				|| string.IsNullOrEmpty(ThisUserName))
				return;

			try
			{
				if (ping == null)
					throw new ArgumentNullException(nameof(ping), "ping == null");

				var new_ping = new PingCommand(ping);

				await Clients.User(OtherUserIdentifier).ServerToClientPing(new_ping);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex.Message);
				throw;
			}
		}

		//[Authorize(Policy = Constants.InkBallViewOtherGamesPolicyName)]
		public async Task<PlayerPointsAndPathsDTO> GetPlayerPointsAndPaths(bool viewOnly, int gameID)
		{
			CancellationToken token = this.Context.ConnectionAborted;

			if (viewOnly)
			{
				//if(!(await authorization.AuthorizeAsync(base.User, Constants.InkBallViewOtherGamesPolicyName)).Succeeded)
				if (!this.Context.User.HasClaim("role", "InkBallViewOtherPlayerGames"))
					throw new UnauthorizedAccessException("Can not watch other player games!");

				var claimsPrincipal = this.Context.User;
				ThisPlayer = await GetPlayer(claimsPrincipal, ThisUserIdentifier, token);

				if (gameID <= 0 || ThisPlayer == null)
					throw new NoGameArgumentNullException("bad player or game");
			}
			else
			{
				await LoadGameAndPlayerStructures(token);
				if (ThisGame == null || ThisPlayer == null || string.IsNullOrEmpty(ThisUserName))
					throw new NoGameArgumentNullException("bad player or game");

				gameID = ThisGame.iId;
			}

			try
			{
				var packed = await _dbContext.LoadPointsAndPathsAsync(gameID, token, true);
				var dto = new PlayerPointsAndPathsDTO(packed.Points, packed.Paths, ThisPlayer);

				return dto;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex.Message);
				throw;
			}
		}

		public async Task<string> GetUserSettings()
		{
			CancellationToken token = this.Context.ConnectionAborted;

			await LoadGameAndPlayerStructures(token);
			if (ThisGame == null || ThisPlayer == null || string.IsNullOrEmpty(ThisUserName))
				throw new NoGameArgumentNullException("bad player or game");

			try
			{
				var settings_json = this.Context.User.FindFirst(c => c.ValueType == "UserSettings" && c.Type == ClaimTypes.UserData);
				if (settings_json != null && settings_json.Value != null)
				{
					var settings = settings_json.Value;
					return settings;
				}
				else
					return null;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex.Message);
				throw;
			}
		}

		public async Task ClientToServerStopAndDraw(StopAndDrawCommand notification)
		{
			CancellationToken token = this.Context.ConnectionAborted;

			await LoadGameAndPlayerStructures(token);
			if (ThisGame == null || ThisPlayer == null || string.IsNullOrEmpty(ThisUserName))
				throw new NoGameArgumentNullException("bad player or game");

			try
			{
				await Clients.User(OtherUserIdentifier).ServerToClientStopAndDraw(notification);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex.Message);
				throw;
			}
		}

		#endregion IGameServer implementation
	}
}
___BEGIN___COMMAND_DONE_MARKER___0
