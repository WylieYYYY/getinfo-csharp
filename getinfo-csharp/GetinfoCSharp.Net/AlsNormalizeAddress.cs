#nullable enable

using System;
using System.Linq;
using System.Text.RegularExpressions;

using WylieYYYY.GetinfoCSharp.Text;

namespace WylieYYYY.GetinfoCSharp.Net
{
	/// <inheritdoc/>
	public partial record AlsLocationInfo
	{
		private const string NumberPrefixRegex = @"NOS?\.? ?";
		private const string UnitNumberRegex = "[0-9]+[A-Z]{0,2}";
		private const string FloorRegex = @"(G|[0-9]+)\/F";
		private static readonly string[] PairValueDelimiters = { "AND", "=", "&" };
		private const string NameNumberRegex = $"^(.* )?({NumberPrefixRegex})?{UnitNumberRegex}";
		private const string NoiseBeforeNumberRegex = $"^.*?(?={UnitNumberRegex})";
		// ALS and region specific normalization regular expressions
		private const string LotRegex = $"LOTS? ?({NumberPrefixRegex})?{UnitNumberRegex}";
		private const string DdRegex = @"D\.?D\.? ?[0-9]+";
		private const string RuralRoadRegexExt = "|KAM TIN SHI|SHEUNG LING PEI|SHEUNG WO CHE";
		private const string HouseOrRoadRegex = ".*(AVENUE|CIRCUIT|COURT|CRESCENT|DRIVE|LANE" +
				"|LAU|PATH|R(OA)?D|STREET|TERRACE|TSUEN|VILLA(GE)?" + RuralRoadRegexExt + ")";
		private const string RuralHouseRegexExt = "|PAK SHE";
		private const string HouseRegex = "(BLOCK|BUILDING|CENT(ER|RE)|CHUEN|COURT" +
				$"|DAI HA|ESTATE|HOUSE|MALL|MANSION|TSUEN{RuralHouseRegexExt})$";

		// TODO: error handling
		private static string? NormalizeAddress(string address)
		{
			address = address.ToUpperInvariant();
			TextUtility.RegexRemove(ref address,
					$"({UnitNumberRegex}, ?)(?={UnitNumberRegex})",           // list of unit numbers
					"\"", $"{LotRegex}.* IN {DdRegex}|{DdRegex},? {LotRegex}" // LOT and DD number
			);
			// use alternative address instead, should be clearer
			Match alternativeMatch = Regex.Match(address, @"(\(ALSO KNOWN AS )([^\)]*)(\)$)");
			if (alternativeMatch.Success) address = alternativeMatch.Groups[2].Value;
			// remove full-width brackets
			TextUtility.RegexRemove(ref address, @"(\uFF08|\().*(\uFF09|\))");
			string[] addressLines = address.Split(',', StringSplitOptions.TrimEntries);
			int largeScopeStartIndex = 0;
			bool hasLargeEnoughScope = false;
			bool shouldKeepSearching = true;
			for (int addressLineIndex = 0; addressLineIndex < addressLines.Length; addressLineIndex++)
			{
				ref string addressLine = ref addressLines[addressLineIndex];
				// remove pair room numbers or floor numbers
				const string RoomOrFloorRegex = $"[A-Z]{{0,2}}{UnitNumberRegex}(/F)?";
				string[] pairRoomOrFloorRegex = (from delimiter in PairValueDelimiters select
						$"({RoomOrFloorRegex} ?{delimiter} ?)(?={RoomOrFloorRegex})").ToArray();
				TextUtility.RegexRemove(ref addressLine, pairRoomOrFloorRegex);
				if (!shouldKeepSearching) continue;
				// do not attempt to parse address with LOT and DD numbers
				string residueLotDdRegexString = $"(( |^){LotRegex}|( |^){DdRegex})";
				if (Regex.Match(addressLine, residueLotDdRegexString).Success) break;
				// remove all floor numbers
				while (Regex.Match(addressLine, FloorRegex).Success)
				{
					string floorWithNameRegex = $@"(.*{FloorRegex} (OF ?)?)(?=\S+)\s*";
					bool containsFloorInLine = Regex.Match(addressLine, floorWithNameRegex).Success;
					TextUtility.RegexRemove(ref addressLine, floorWithNameRegex);
					if (containsFloorInLine)
					{
						/* may be a floor with a building name, it will be large enough if so.
						   If there are more in the next line, invalidate this line.
						   As it may be a false positive. */
						largeScopeStartIndex = addressLineIndex;
						hasLargeEnoughScope = true;
					}
					else
					{
						// no name attached to the floor number, nothing of value
						largeScopeStartIndex = addressLineIndex + 1;
						break;
					}
				}
				// a number with a house or road name
				if (Regex.Match(addressLine, NameNumberRegex + HouseOrRoadRegex).Success)
				{
					TextUtility.RegexRemove(ref addressLine, NoiseBeforeNumberRegex);
					largeScopeStartIndex = addressLineIndex;
					shouldKeepSearching = false;
					continue;
				}
				// room or house number on the current line, and house or street name on the next one
				if (Regex.Match(addressLine, NameNumberRegex +'$').Success &&
						addressLines.Length != addressLineIndex + 1 &&
						Regex.Match(addressLines[addressLineIndex + 1], HouseOrRoadRegex).Success)
				{
					ref string nextAddressLine = ref addressLines[addressLineIndex + 1];
					TextUtility.RegexRemove(ref addressLine, NoiseBeforeNumberRegex);
					// combine the lines and pick it up in the next loop
					nextAddressLine = addressLine + ' ' + nextAddressLine;
				}
				// house names that do not need a house number
				if (Regex.Match(addressLine, HouseRegex).Success)
				{
					// remove undetected sub-units
					TextUtility.RegexRemove(ref addressLine, ".* OF ", $".*{UnitNumberRegex} ");
					largeScopeStartIndex = addressLineIndex;
					hasLargeEnoughScope = true;
					continue;
				}
				// remove 'at' that appears before street names or house names
				TextUtility.RegexRemove(ref addressLine, ".* AT ");
			}
			if (!hasLargeEnoughScope && shouldKeepSearching) return null;
			addressLines = (from line in addressLines.Skip(largeScopeStartIndex)
					where line != string.Empty select line).ToArray();
			return string.Join(", ", addressLines);
		}
	}
}
