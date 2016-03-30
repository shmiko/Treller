using System.ComponentModel;

namespace SKBKontur.Treller.WebApplication.Implementation.VirtualMachines.BusinessObjects
{
    public enum VirtualMachineProfile
    {
        [Description("�������� ������")]
        Stand,

        [Description("�������������� � ���� �����")]
        Ci,

        [Description("�������������� �����")]
        FunctionalCi,

        [Description("3 ����")]
        ThreeNode,

        [Description("���� ������ 3 �����")]
        BuildServer,

        [Description("Cassanrda + Elastic 3 �����")]
        ThreeNodeJava,
    }
}