using System;
using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Invert.Core.GraphDesigner;
using Invert.uFrame.MVVM;
using uFrame.Graphs;
using UniRx;
using UnityEngine;

[TemplateClass(MemberGeneratorLocation.Both, ClassNameFormat = uFrameFormats.CONTROLLER_FORMAT)]
public partial class ControllerTemplate : Controller, IClassTemplate<ElementNode>
{
    public TemplateContext<ElementNode> Ctx { get; set; }

    public string OutputPath
    {
        get { return Path2.Combine(Ctx.Data.Graph.Name, "Controllers"); }
    }

    public bool CanGenerate
    {
        get { return true; }
    }

    public void TemplateSetup()
    {
        Ctx.TryAddNamespace("UniRx");
        
        if (Ctx.IsDesignerFile)
        {
            Ctx.CurrentDecleration.Attributes = MemberAttributes.Abstract;
            //if (Ctx.Data.BaseNode == null)
            //{
            //    Ctx.SetBaseType("Controller<{0}>", Ctx.Data.Name.AsViewModel());
            //}
        }

        Ctx.AddIterator("CommandMethod", _ => _.Commands.Where(p => string.IsNullOrEmpty(p.RelatedTypeName)));
        Ctx.AddIterator("CommandMethodWithArg", _ => _.Commands.Where(p => !string.IsNullOrEmpty(p.RelatedTypeName)));
        if (Ctx.Data.BaseNode == null)
        {
            Ctx.AddIterator("InstanceProperty",
                _ => _.GetParentNodes().OfType<SubsystemNode>().SelectMany(p => p.Instances).Distinct());
        }
        else
        {
            Ctx.AddCondition("InstanceProperty",_=>false);
        }

        
        //Ctx.AddIterator("ControllerProperty", _ =>{});
    }

    public string NameAsViewModel { get { return Ctx.Data.Name.AsViewModel(); } }

    
    [TemplateProperty(MemberGeneratorLocation.DesignerFile, AutoFill = AutoFillType.NameAndTypeWithBackingField,NameFormat = "{0}Manager")]
    public IViewModelManager ViewModelManager
    {
        get
        {
            Ctx.SetType(typeof(IViewModelManager)); // I force this so it doesn't change it
            Ctx.CurrentProperty.CustomAttributes.Add(new CodeAttributeDeclaration(typeof(InjectAttribute).ToCodeReference(), new CodeAttributeArgument(new CodePrimitiveExpression(Ctx.Data.Name))));
            return null;
        }
    }

    [TemplateProperty(MemberGeneratorLocation.DesignerFile, AutoFillType.NameAndTypeWithBackingField)]
    public ViewModel InstanceProperty
    {
        get
        {
            Ctx.CurrentProperty.CustomAttributes.Add(new CodeAttributeDeclaration(
                typeof(InjectAttribute).ToCodeReference(),
                new CodeAttributeArgument(new CodePrimitiveExpression(Ctx.ItemAs<InstancesReference>().Name))
                ));

            return null;
        }
        set
        {

        }
    }

    // Controller properties removed in 1.6
    //[TemplateProperty(MemberGeneratorLocation.DesignerFile, AutoFillType.NameAndTypeWithBackingField)]
    //public ViewModel ControllerProperty
    //{
    //    get
    //    {
    //        var name = Ctx.Item.Name.AsController();
    //        Ctx.SetType(name);
    //        Ctx.CurrentProperty.Name = name;
    //        Ctx.AddAttribute("Inject");
    //        return null;
    //    }
    //    set
    //    {

    //    }
    //}

    [TemplateMethod(MemberGeneratorLocation.Both)]
    public override void Setup()
    {
        base.Setup();
        Ctx._comment("This is called when the controller is created");
    }

    [TemplateProperty(MemberGeneratorLocation.DesignerFile, AutoFillType.NameAndType, NameFormat = "{0}ViewModels")]
    public IEnumerable<ViewModel> ViewModelItems
    {
        get
        {
            Ctx.SetTypeArgument(Ctx.Data.Name.AsViewModel());
            Ctx._("return {1}Manager.OfType<{0}>()",Ctx.Data.Name.AsViewModel(), Ctx.Data.Name);
            return null;
        }
    }
    
    [TemplateMethod(MemberGeneratorLocation.DesignerFile, CallBase = true)]
    public override void Initialize(ViewModel viewModel)
    {
        Ctx._comment("This is called when a viewmodel is created");
        if (!Ctx.IsDesignerFile) return;
        Ctx._("this.Initialize{0}((({1})(viewModel)))", Ctx.Data.Name, NameAsViewModel);
    }

    [TemplateMethod("Create{0}", MemberGeneratorLocation.DesignerFile, false)]
    public ViewModel CreateElement()
    {
        Ctx.SetType(NameAsViewModel);
        Ctx._("return (({0})(this.Create()))", NameAsViewModel);
        return null;
    }

    [TemplateMethod(MemberGeneratorLocation.DesignerFile, false)]
    public override ViewModel CreateEmpty()
    {
        Ctx._("return new {0}(this)", NameAsViewModel);
        return null;
    }

    [TemplateMethod("Initialize{0}", MemberGeneratorLocation.Both, true)]
    public virtual void InitializeElement(ViewModel viewModel)
    {
        Ctx._comment("This is called when a {0} is created", NameAsViewModel);
        Ctx.CurrentMethod.Parameters[0].Type = new CodeTypeReference(NameAsViewModel);
        if (Ctx.IsDesignerFile)
            Ctx._("{0}Manager.Add(viewModel)", Ctx.Data.Name);
    }

    [TemplateMethod( MemberGeneratorLocation.DesignerFile, true)]
    public override void DisposingViewModel(ViewModel viewModel)
    {
        base.DisposingViewModel(viewModel);
        Ctx._("{0}Manager.Remove(viewModel)",Ctx.Data.Name);
    }

    [TemplateMethod("{0}", MemberGeneratorLocation.Both, true)]
    public virtual void CommandMethod(ViewModel viewModel)
    {
        Ctx.CurrentMethod.Parameters[0].Type = new CodeTypeReference(Ctx.Data.Name + "ViewModel");
        DoTransition();
    }

    private void DoTransition()
    {
        if (Ctx.IsDesignerFile)
        {
            var transition = Ctx.Item.OutputTo<TransitionsChildItem>();
            if (transition != null)
            {

                var stateMachineProperty =
                    Ctx.Data.LocalProperties.FirstOrDefault(p => p.RelatedTypeNode is StateMachineNode);

                if (stateMachineProperty != null)
                {
                    Ctx._("viewModel.{0}.Transition(\"{1}\")", stateMachineProperty.Name.AsSubscribableProperty(),
                        transition.Name);
                }
            }
        }
    }

    [TemplateMethod("{0}", MemberGeneratorLocation.Both, true)]
    public virtual void CommandMethodWithArg(ViewModel viewModel, object arg)
    {
        CommandMethod(viewModel);
        Ctx.CurrentMethod.Parameters[1].Type = new CodeTypeReference(Ctx.TypedItem.RelatedTypeName);
        DoTransition();

    }
}
