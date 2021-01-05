using FergusonSourcingCore.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FergusonSourcingEngine.Controllers
{
    public class RequirementController
    {
        private ItemController itemController { get; set; }
        private static ILogger _logger;

        public RequirementController(ILogger logger, ItemController itemController)
        {
            _logger = logger;
            this.itemController = itemController;
        }


        /// <summary>
        ///     Determines if the specified location meets the item's sourcing requirements.
        /// </summary>
        /// <returns>true if the location meets the item's sourcing requirments.</returns>
        public bool DoesLocationMeetRequirements(SingleLine line, string guide, Location location)
        {
            var requirementDict = line.Requirements;
            var itemData = itemController.items.ItemDict[line.MasterProductNumber];

            // Sourcing guide
            if (!MeetsSourcingGuidelineRequirement(location, guide))
                return false;

            // Overpack
            requirementDict.TryGetValue("Overpack", out bool overPackRequired);

            if (overPackRequired && !MeetsOverPackRequirements(itemData, line.Quantity, location)) 
                return false;

            return true;
        }


        /// <summary>
        ///     Returns true if the location is available as a potential location to source the item from.
        /// </summary>
        /// <param name="location">Location data, including the location type (DC, Branch, SOD, etc.)</param>
        /// <param name="guide">Sourcing guide on the item.</param>
        public bool MeetsSourcingGuidelineRequirement(Location location, string guide)
        {
            // FEI lines can source from branches or DC's
            if (guide == "FEI" && (location.DCLocation || location.BranchLocation))
                return true;

            if (guide == "Branch" && location.BranchLocation)
                return true;

            if (guide == "SOD" && location.SODLocation)
                return true;

            return false;
        }


        /// <summary>
        ///     If the quantity on the line is less than or equal to the ground qty threshold of the PSM, then the location must be overpack capable
        /// </summary>
        /// <param name="itemData">Item data, including preferred shipping method and overpack required.</param>
        /// <param name="quantity">Line item quantity on the order.</param>
        /// <param name="location">Location data object, including overpack capable.</param>
        /// <returns>true if the location meets the item's overpack requirments.</returns>
        public bool MeetsOverPackRequirements(ItemData itemData, int quantity, Location location)
        {
            if (!itemData.OverpackRequired || itemData.PreferredShippingMethod == "LTL") 
                return true;

            // If the item is going to ship ground, then the location must be overpack capable.
            if (!location.OverpackCapable)
                return false;

            return true;
        }


        /// <summary>
        ///     Adds an entry for each requirement to the line requirments dict.
        /// </summary>
        /// <param name="line">Current line being sourced.</param>
        /// <param name="atgOrderRes">The ATG Order response that will be written to CosmosDB.</param>
        public void SetLineRequirements(SingleLine line, AtgOrderRes atgOrderRes)
        {
            if (LineRequiresOverpackLocation(line.MasterProductNumber, line.Quantity, atgOrderRes))
            {
                line.Requirements.Add("Overpack", true);
            }

            // Set requirements on the AtgOrder for use when setting sourcing messages
            atgOrderRes.items.FirstOrDefault(i => i.lineId == line.LineId).requirements = line.Requirements;
        }


#region Individual Requirements
        /// <summary>
        ///     If the item is going to ship ground, then the location must be overpack capable.
        /// </summary>
        /// <param name="mpn">Master Product Number on the item.</param>
        /// <param name="quantity">Item quantity on the order.</param>
        /// <returns>true if line must be sourced from an overpack capable location.</returns>
        public bool LineRequiresOverpackLocation(string mpn, int quantity, AtgOrderRes atgOrderRes)
        {
            var item = atgOrderRes.items.FirstOrDefault(i => i.masterProdId == mpn);
            
            if (item?.preferredShippingMethod == "LTL") 
                return false;
            
            var itemData = itemController.items.ItemDict[mpn];

            if(!itemData.OverpackRequired) 
                return false;

            if (itemData == null) 
                throw new ArgumentNullException("itemData");

            if (quantity <= itemData.GroundQuantityThreshold || itemData.GroundQuantityThreshold == 0)
                return true;

            return false;
        }
#endregion
    }
}
