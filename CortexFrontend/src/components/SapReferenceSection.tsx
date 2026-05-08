import { useState } from "react";
import SapReferenceCatalogPage from "./SapReferenceCatalogPage";
import SapReferencePage from "./SapReferencePage";
import { ConfigSecondaryButton } from "./configurationAdminUi";

type SapTab = "catalog" | "manage";

export default function SapReferenceSection() {
  const [tab, setTab] = useState<SapTab>("catalog");

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap gap-2">
        <ConfigSecondaryButton
          type="button"
          className={tab === "catalog" ? "ring-2 ring-cortex-blue/40" : ""}
          onClick={() => setTab("catalog")}
        >
          Catalog
        </ConfigSecondaryButton>
        <ConfigSecondaryButton
          type="button"
          className={tab === "manage" ? "ring-2 ring-cortex-blue/40" : ""}
          onClick={() => setTab("manage")}
        >
          Manage reference data
        </ConfigSecondaryButton>
      </div>
      {tab === "catalog" ? <SapReferenceCatalogPage /> : <SapReferencePage />}
    </div>
  );
}
