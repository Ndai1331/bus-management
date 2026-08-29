namespace HCS.Permissions;

public static class HCSPermissions
{
    public const string GroupName = "HCS";

    public static class Languages
    {
        public const string Default = GroupName + ".Languages";
        public const string Create = Default + ".Create";
        public const string Update = Default + ".Update";
        public const string Delete = Default + ".Delete";
        public const string ManageTexts = Default + ".ManageTexts";
    }

    public static class AuditViewer
    {
        public const string Default = GroupName + ".AuditViewer";
    }

    public static class Organization
    {
        public const string Default = HCSOrganizationPermissions.Group;
        public const string Departments = HCSOrganizationPermissions.Departments;
        public const string Units = HCSOrganizationPermissions.Units;
        public const string Positions = HCSOrganizationPermissions.Positions;
        public const string MasterData = HCSOrganizationPermissions.MasterData;
        public const string UserMappings = HCSOrganizationPermissions.UserMappings;

        public static readonly string[] AdministrationPermissions = HCSOrganizationPermissions.AdministrationPermissions;
    }

    public static class Catalogs
    {
        public const string Default = HCSCatalogPermissions.Group;
        public const string MasterData = HCSCatalogPermissions.MasterData;
        public const string DocumentTypes = HCSCatalogPermissions.DocumentTypes;
        public const string Sectors = HCSCatalogPermissions.Sectors;
        public const string UrgencyLevels = HCSCatalogPermissions.UrgencyLevels;
        public const string ConfidentialityLevels = HCSCatalogPermissions.ConfidentialityLevels;
        public const string ProcessingMethods = HCSCatalogPermissions.ProcessingMethods;
        public const string DocumentStatuses = HCSCatalogPermissions.DocumentStatuses;
        public const string SigningMethods = HCSCatalogPermissions.SigningMethods;
        public const string EventTypes = HCSCatalogPermissions.EventTypes;
    }

    // These permission names are consumed by the standalone Work Management API.
    // They live in the shared authorization catalog so roles can be managed in
    // the central ABP Roles UI and emitted into BFF access tokens.
    public static class WorkManagement
    {
        public const string Default = "WorkManagement";
        public const string Projects = Default + ".Projects";
        public const string Tasks = Default + ".ProjectTasks";
        public const string Calendar = Default + ".Calendar";
        public const string Surveys = Default + ".Surveys";
        public const string SurveyManagement = Default + ".SurveyManagement";
        public const string Reports = Default + ".Reports";
        public const string Dashboard = Default + ".Dashboard";

        public static readonly string[] All =
        [
            Projects,
            Tasks,
            Calendar,
            Surveys,
            SurveyManagement,
            Reports,
            Dashboard
        ];
    }

    public static class Documents
    {
        public const string Default = "Documents";
        public const string View = Default + ".View";
        public const string Create = Default + ".Create";
        public const string Update = Default + ".Update";
        public const string Assign = Default + ".Assign";
        public const string ManageFiles = Default + ".ManageFiles";
        public const string WorkflowView = Default + ".Workflow.View";
        public const string WorkflowManage = Default + ".Workflow.Manage";
        public const string WorkflowStart = Default + ".Workflow.Start";
        public const string WorkflowDecide = Default + ".Workflow.Decide";
        public const string SigningConfigure = Default + ".Signing.Configure";
        public const string SigningExecute = Default + ".Signing.Execute";
        public const string SigningReport = Default + ".Signing.Report";

        public static readonly string[] All =
        [
            View,
            Create,
            Update,
            Assign,
            ManageFiles,
            WorkflowView,
            WorkflowManage,
            WorkflowStart,
            WorkflowDecide,
            SigningConfigure,
            SigningExecute,
            SigningReport
        ];
    }

    public static class Collaboration
    {
        public const string Default = "Collaboration";
        public const string Chat = Default + ".Chat";
        public const string Notifications = Default + ".Notifications";
        public const string Administration = Default + ".Administration";
    }

    public static class BusManagement
    {
        public const string Default = "HCS.BusManagement";
        public const string Stations = Default + ".Stations";
        public const string MasterData = Default + ".MasterData";
        public const string OperatorsContracts = Default + ".OperatorsContracts";
        public const string FleetCompliance = Default + ".FleetCompliance";
        public const string Departures = Default + ".Departures";
        public const string Revenue = Default + ".Revenue";
        public const string Expenses = Default + ".Expenses";
        public const string Premises = Default + ".Premises";
        public const string Reconciliation = Default + ".Reconciliation";
        public const string ReconciliationCheck = Reconciliation + ".Check";
        public const string ReconciliationClose = Reconciliation + ".Close";
        public const string ReconciliationAdjust = Reconciliation + ".Adjust";
        public const string ReconciliationAdjustApprove = Reconciliation + ".AdjustApprove";
        public const string Reports = Default + ".Reports";
        public const string StationAssignments = Default + ".StationAssignments";
        public const string StationsCreate = Stations + ".Create";
        public const string StationsUpdate = Stations + ".Update";
        public const string DeparturesUpdate = Departures + ".Update";
        public const string MasterDataCreate = MasterData + ".Create";
        public const string OperatorsContractsCreate = OperatorsContracts + ".Create";
        public const string FleetComplianceCreate = FleetCompliance + ".Create";
        public const string FleetComplianceUpdate = FleetCompliance + ".Update";
        public const string DeparturesCreate = Departures + ".Create";
        public const string RevenueCreate = Revenue + ".Create";
        public const string ExpensesCreate = Expenses + ".Create";
        public const string ExpensesApprove = Expenses + ".Approve";
        public const string PremisesCreate = Premises + ".Create";
        public const string ReconciliationCreate = Reconciliation + ".Create";
        public const string ReconciliationApprove = Reconciliation + ".Approve";
        public const string ReportsExport = Reports + ".Export";
        public const string StationAssignmentsCreate = StationAssignments + ".Create";

        public static readonly string[] All =
        [
            Stations, MasterData, OperatorsContracts, FleetCompliance, Departures, Revenue,
            Expenses, Premises, Reconciliation, ReconciliationCheck, ReconciliationClose,
            ReconciliationAdjust, ReconciliationAdjustApprove, Reports, StationAssignments, StationsCreate, StationsUpdate, MasterDataCreate,
            OperatorsContractsCreate, FleetComplianceCreate, FleetComplianceUpdate, DeparturesCreate, RevenueCreate, ExpensesCreate,
            ExpensesApprove, PremisesCreate, ReconciliationCreate, ReconciliationApprove, ReportsExport,
            StationAssignmentsCreate, DeparturesUpdate
        ];
    }
}
