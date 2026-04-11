store-category-contractor-hub = Contractor Hub
store-currency-display-contractor-reputation = Rep

contractor-item-uplink-name = contractor uplink
contractor-item-uplink-desc = A dedicated receiver for contractor contracts, reputation requisitions, and falsefire deployment.
contractor-item-falsefire-portal-name = falsefire portal
contractor-item-falsefire-portal-desc = A one-way Syndicate falsefire aperture keyed to a single hostage.
contractor-item-falsefire-flare-name = contractor falsefire flare
contractor-item-falsefire-flare-desc = A contractor-issued falsefire flare. Ignite it to open a one-way transfer aperture.
contractor-item-stunbaton-name = contractor baton
contractor-item-stunbaton-desc = A compact stun baton issued to contractors for target control during extraction.
contractor-item-hardsuit-name = contractor hardsuit
contractor-item-hardsuit-desc = A discreet Syndicate hardsuit tailored for snatch-and-extract work rather than open battle.
contractor-item-hardsuit-helmet-name = contractor hardsuit helmet
contractor-item-hardsuit-helmet-desc = A contractor helmet with full vacuum sealing and a low-profile silhouette.
contractor-item-lighter-name = contractor flippo lighter
contractor-item-lighter-desc = A rugged lighter stamped for field contractors.
contractor-item-pinpointer-name = contractor pinpointer
contractor-item-pinpointer-desc = A contractor-grade pinpointer for finding targets and extraction equipment.
contractor-item-balloon-name = contractor balloon
contractor-item-balloon-desc = A black-market balloon handed out to contractors who make it back with breathing cargo.
contractor-item-guide-name = contractor field guide
contractor-item-guide-desc = A single-page briefing for newly upgraded Syndicate contractors.
contractor-item-loadout-box-name = contractor loadout box
contractor-item-loadout-box-desc = A compact contractor issue box containing disguise and field utility gear.
contractor-item-extras-box-name = contractor surplus box
contractor-item-extras-box-desc = A box with a few low-profile Syndicate tools for a fresh contractor.
contractor-item-extraction-box-name = contractor extraction kit
contractor-item-extraction-box-desc = A kit with a standard Fulton beacon and spare extraction packs for moving cargo or living targets.
contractor-item-kit-box-name = contractor kit
contractor-item-kit-box-desc = A contractor conversion package for selected Syndicate agents, with uplink access to contracts, falsefire, and requisitions.

uplink-contractor-kit-name = contractor kit
uplink-contractor-kit-desc = Available only to invited agents. Converts a traitor into a Contractor while keeping their original objectives, and unlocks a separate uplink for contracts, falsefire, and reputation requisitions.

contractor-hub-pinpointer-name = contractor pinpointer
contractor-hub-pinpointer-desc = A contractor-grade universal pinpointer for tracking selected equipment or people.

contractor-hub-zippo-name = contractor lighter
contractor-hub-zippo-desc = A durable lighter stamped for black-market field work.

contractor-hub-extraction-name = extraction kit
contractor-hub-extraction-desc = A Fulton beacon and spare extraction packs for beacon-based pickups.

contractor-hub-balloon-name = contractor balloon
contractor-hub-balloon-desc = An extravagant reward for contractors who come back with breathing merchandise.

contractor-guide-content = Congratulations on your promotion to contractor status.

      You now have access to a second Syndicate workflow: paid kidnappings with target transfer through falsefire.

      Included equipment:
      Contractor uplink for contract offers, falsefire, and reputation requisitions.
      Loadout box with disguise gear, agent ID, syndicate encryption key, cigarettes, lighter, and mirror.
      Contractor hardsuit and baton for low-profile field work.

      Standard procedure:
      1. Pick one of the six contracts in the Contractor uplink.
      2. Bring the target to the assigned stationary beacon on the station.
      3. Once confirmed, open falsefire through the uplink.
      4. Push only the assigned target through the portal.

      Operational notes:
      Original traitor objectives remain active.
      A living target pays full telecrystals and 2 reputation.
      A dead target only pays 20% telecrystals, but reputation is still granted.
      More dangerous beacon sectors pay better.

roles-antag-contractor-name = Contractor
roles-antag-contractor-objective = Fulfill paid Syndicate snatch contracts and deliver the target alive to your Fulton beacon.
role-subtype-contractor = contractor
contractor-role-briefing = You are a Contractor. Original traitor objectives stay active, while new snatch work is issued through the Contractor uplink: pick a contract, deliver the target to the assigned beacon, and transfer them through falsefire.
contractor-role-greeting = Purchasing the kit promoted you to Contractor. Original traitor objectives remain. Pick a contract in the uplink, deliver the target to the assigned beacon, open falsefire, and send the target through the portal.
objective-issuer-contractor = Contractor Hub
contractor-survive-objective-name = Survive.
contractor-survive-objective-desc = Dead contractors do not get paid.
contractor-extract-objective-title = Deliver {$targetName}, {$job}, alive to your Fulton beacon.
contractor-extract-objective-desc = Secure the target alive, then bring or fulton them onto an unfolded Fulton beacon.
contractor-contract-complete = Contract complete. Payment received: +{$amount} TC and +{$reputation} reputation.

contractor-offer-available = Central Syndicate has marked you as a potential Contractor. A Contractor kit for 20 TC is now available in your uplink.
contractor-uplink-denied = The Contractor uplink does not respond to your fingerprint.
contractor-contract-accepted = Contract accepted: {$target}, {$job}. Difficulty: {$difficulty}. Transfer point: {$location}.
contractor-contract-declined = The contract on {$target} was declined and will not return.
contractor-beacon-reached = The target has reached beacon {$location}. Falsefire is now unlocked in the Contractor uplink.
contractor-falsefire-issued = A falsefire flare has been dispensed. Ignite it to open the portal.
contractor-falsefire-ignited = Falsefire ignition confirmed. The portal will open in a few seconds.
contractor-falsefire-opened = Falsefire opened. The portal is active and only the assigned target can enter.

contractor-news-author = station informant
contractor-news-title = {$target} has vanished from the station
contractor-news-content = According to the newsroom, {$target}, {$job}, has been abducted. The leaked motive states: {$reason}

contractor-target-job-unknown = unknown assignment

contractor-status-idle = No active contract. Available offers: {$count}.
contractor-status-awaiting-beacon = Active contract awaiting beacon hand-off at {$location}.
contractor-status-portal-ready = The target is at the beacon. Dispense falsefire through the uplink.
contractor-status-falsefire-issued = Falsefire dispensed. Ignite the flare.
contractor-status-falsefire-arming = Falsefire is arming. The portal will open in a few seconds.
contractor-status-portal-open = The falsefire portal is open and ready for target transfer.

contractor-reason-easy-1 = {$target} heard too much about grey-market traffic through {$location}.
contractor-reason-easy-2 = {$target} is needed for questioning about service routes around {$location}.
contractor-reason-easy-3 = {$target} maintains useful habits and contacts in {$location}.
contractor-reason-medium-1 = Syndicate wants {$target}: logistics of interest pass through {$location}.
contractor-reason-medium-2 = {$target} is considered a technical source tied to {$location}.
contractor-reason-medium-3 = An order was placed on {$target} for restricted knowledge related to {$location}.
contractor-reason-hard-1 = {$target} has access that cannot be bought. Capture via {$location}.
contractor-reason-hard-2 = Syndicate believes {$target} knows too much about the secured sector {$location}.
contractor-reason-hard-3 = {$target} is needed for leverage against the command chain. Transfer point: {$location}.

contractor-uplink-window-title = Contractor Uplink
contractor-uplink-active-title = Active Contract
contractor-uplink-offers-title = Offers
contractor-uplink-requisitions-title = Requisitions
contractor-uplink-reputation = Reputation: {$amount}
contractor-uplink-no-active = No active contract selected.
contractor-uplink-no-offers = No new offers are currently available.
contractor-uplink-dispense-falsefire = Dispense falsefire
contractor-uplink-accept = Accept
contractor-uplink-decline = Decline
contractor-uplink-buy = Buy for {$cost} rep.
contractor-uplink-contract-header = {$target}, {$job}. Difficulty: {$difficulty}
contractor-uplink-contract-target-header = {$target}, {$job}
contractor-uplink-difficulty-label = Difficulty: {$difficulty}
contractor-uplink-accept-difficulty = Accept: {$difficulty}
contractor-uplink-location = Beacon: {$location}
contractor-uplink-payout = Payout: {$alive} TC alive, {$dead} TC dead.

contractor-difficulty-easy = easy
contractor-difficulty-medium = medium
contractor-difficulty-hard = hard

contractor-requisition-pinpointer-name = contractor pinpointer
contractor-requisition-pinpointer-desc = A general-purpose pinpointer for finding targets and navigating the station.
contractor-requisition-reroll-name = Refresh Contracts
contractor-requisition-reroll-desc = Replaces inactive contracts with a fresh batch of candidates.
contractor-requisition-extraction-name = Fulton Extraction Kit
contractor-requisition-extraction-desc = A beacon and fulton set for safely sending a captive to the drop point.
contractor-requisition-zippo-name = contractor lighter
contractor-requisition-zippo-desc = A dependable black-market field lighter.
contractor-requisition-balloon-name = contractor balloon
contractor-requisition-balloon-desc = A gaudy reward for a contractor who finished the job.