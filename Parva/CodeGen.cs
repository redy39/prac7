// Code Generation for compilers and assemblers targetting the PVM (C#)
// P.D. Terry, Rhodes University, 2017
// As supplied for the last practical
// 2017/08/29

using System;
using System.IO;

namespace Parva {

  class Label {
    private int memAdr;      // address if this.defined, else last forward reference
    private bool defined;    // true once this.memAdr is known

    public Label(bool known) {
    // Constructor for label, possibly at already known location
      if (known) this.memAdr = CodeGen.GetCodeLength();
      else this.memAdr = CodeGen.undefined;  // mark end of forward reference chain
      this.defined = known;
    } // constructor

    public int Address() {
    // Returns memAdr if known, otherwise effectively adds to a forward reference
    // chain that will be resolved if and when Here() is called and returns the
    // address of the most recent forward reference
      int adr = memAdr;
      if (!defined) memAdr = CodeGen.GetCodeLength();
      return adr;
    } // Label.Address

    public void Here() {
    // Defines memAdr of this label to be at current location counter after fixing
    // any outstanding forward references
      if (defined) Parser.SemError("Compiler error - bad label");
      else CodeGen.BackPatch(memAdr);
      memAdr = CodeGen.GetCodeLength();
      defined = true;
    } // Label.Here

    public bool IsDefined() {
    // Returns true if the location of this label has been established
      return defined;
    } // Label.IsDefined

    public override string ToString() {
      return memAdr.ToString();
    } // Label.ToString

  } // end Label

  class CodeGen {
    static bool generatingCode = true;
    static int codeTop = 0, stkTop = PVM.memSize;

    public const int
      undefined  = -1,
      headerSize = PVM.headerSize,

      nop  =  1,
      add  =  2,
      sub  =  3,
      mul  =  4,
      div  =  5,
      rem  =  6,
      and  =  7,
      or   =  8,
      ceq  =  9,
      cne  = 10,
      clt  = 11,
      cge  = 12,
      cgt  = 13,
      cle  = 14;

    private static void Emit(int word) {
    // Code generator for single word
      if (!generatingCode) return;
      if (codeTop >= stkTop) {
        Parser.SemError("program too long"); generatingCode = false;
      }
      else {
        PVM.mem[codeTop] = word; codeTop++;
      }
    } // CodeGen.Emit

    public static void NegateInteger() {
    // Generates code to negate integer value on top of evaluation stack
      Emit(PVM.neg);
    } // CodeGen.NegateInteger

    public static void NegateBoolean() {
    // Generates code to negate boolean value on top of evaluation stack
      Emit(PVM.not);
    } // CodeGen.NegateBoolean

    public static void BinaryOp(int op) {
    // Generates code to pop two values A,B from evaluation stack
    // and push value A op B
      switch (op) {
        case CodeGen.mul:  Emit(PVM.mul); break;
        case CodeGen.div:  Emit(PVM.div); break;
        case CodeGen.rem:  Emit(PVM.rem); break;
        case CodeGen.and:  Emit(PVM.and); break;
        case CodeGen.add:  Emit(PVM.add); break;
        case CodeGen.sub:  Emit(PVM.sub); break;
        case CodeGen.or :  Emit(PVM.or);  break;
      }
    } // CodeGen.BinaryOp

    public static void BooleanOp(Label branch, int op) {
    // Generates code for short circuit Boolean operator op
      switch (op) {
        case CodeGen.and:  Emit(PVM.bfalse); break;
        case CodeGen.or :  Emit(PVM.btrue); break;
      }
      Emit(branch.Address());
    } // CodeGen.BooleanOp

    public static void Comparison(int op) {
    // Generates code to pop two values A,B from evaluation stack
    // and push Boolean value A op B
      switch (op) {
        case CodeGen.ceq:  Emit(PVM.ceq); break;
        case CodeGen.cne:  Emit(PVM.cne); break;
        case CodeGen.clt:  Emit(PVM.clt); break;
        case CodeGen.cle:  Emit(PVM.cle); break;
        case CodeGen.cgt:  Emit(PVM.cgt); break;
        case CodeGen.cge:  Emit(PVM.cge); break;
        case CodeGen.nop:  break;
      }
    } // CodeGen.Comparison

    public static void Read(int type) {
    // Generates code to read a value of specified type
    // and store it at the address found on top of stack
      switch (type) {
        case Types.intType:  Emit(PVM.inpi); break;
        case Types.boolType: Emit(PVM.inpb); break;
      }
    } // CodeGen.Read

    public static void ReadLine() {
    // Generates code to skip to next line of data
      Emit(PVM.inpl);
    } // CodeGen.ReadLine

    public static void Write(int type) {
    // Generates code to output value of specified type from top of stack
      switch (type) {
        case Types.intType:  Emit(PVM.prni); break;
        case Types.boolType: Emit(PVM.prnb); break;
      }
    } // CodeGen.Write

    public static void WriteLine() {
    // Generates code to output line mark
      Emit(PVM.prnl);
    } // CodeGen.WriteLine

    public static void WriteString(string str) {
    // Generates code to output a string str stored at known location
      int l = str.Length, first = stkTop - 1;
      if (stkTop <= codeTop + l + 1) {
        Parser.SemError("program too long"); generatingCode = false;
        return;
      }
      for (int i = 0; i < l; i++) {
        stkTop--; PVM.mem[stkTop] = str[i];
      }
      stkTop--; PVM.mem[stkTop] = 0;
      Emit(PVM.prns); Emit(first);
    } // CodeGen.WriteString

    public static void LoadConstant(int number) {
    // Generates code to push number onto evaluation stack
      Emit(PVM.ldc); Emit(number);
    } // CodeGen.LoadConstant

    public static void LoadAddress(Entry var) {
    // Generates code to push address of variable var onto evaluation stack
      Emit(PVM.lda);
      Emit(var.offset);
    } // CodeGen.LoadAddress

    public static void LoadValue(Entry var) {
    // Generates code to push value of variable var onto evaluation stack
      Emit(PVM.ldl); Emit(var.offset);
    } // CodeGen.LoadValue

    public static void Index() {
    // Generates code to index an array on the heap
      Emit(PVM.ldxa);
    } // CodeGen.Index

    public static void Allocate() {
    // Generates code to allocate an array on the heap
      Emit(PVM.anew);
    } // CodeGen.Allocate

    public static void Dereference() {
    // Generates code to replace top of evaluation stack by the value found at the
    // address currently stored on top of the stack
      Emit(PVM.ldv);
    } // CodeGen.Dereference

    public static void Assign(int type) {
    // Generates code to store value currently on top-of-stack on the address
    // given by next-to-top, popping these two elements
      Emit(PVM.sto);
    } // CodeGen.Assign

    public static void LoadReturnAddress() {
    // Generates code to push address of function return slot on top of stack.
      Emit(PVM.lda); Emit(0);
    } // CodeGen.LoadReturnAddress

    public static void StoreValue(Entry var) {
    // Generates code to pop top of stack and store at known offset
      Emit(PVM.stl); Emit(var.offset);
    } // CodeGen.StoreValue

    public static void StoreReturnValue() {
    // Generates code to pop top of stack and store in the return field of the frame
      Emit(PVM.stl); Emit(0);
    } // CodeGen.StoreReturnValue

    public static void OpenStackFrame(int size) {
    // Generates (possibly incomplete) code to reserve space for local variables
      Emit(PVM.dsp); Emit(size);
    } // CodeGen.OpenStackFrame

    public static void FixDSP(int location, int size) {
    // Fixes up DSP instruction at location to reserve size space for variables
      PVM.mem[location+1] = size;
    } // CodeGen.FixDSP

    public static void LeaveProgram() {
    // Generates code needed to leave a program (halt)
      Emit(PVM.halt);
    } // CodeGen.LeaveProgram

    public static void Branch(Label destination) {
    // Generates unconditional branch to destination
      Emit(PVM.brn); Emit(destination.Address());
    } // CodeGen.Branch

    public static void BranchFalse(Label destination) {
    // Generates branch to destination, conditional on the Boolean
    // value currently on top of the evaluation stack, popping this value
      Emit(PVM.bze); Emit(destination.Address());
    } // CodeGen.BranchFalse

    public static void FrameHeader() {
    // Generates code to allocate standard stack frame header
      Emit(PVM.fhdr);
    } // CodeGen.FrameHeader

    public static void Call(Label entryLabel) {
    // Generates code to call a function
      Emit(PVM.call); Emit(entryLabel.Address());
    } // CodeGen.Call

    public static void LeaveVoidFunction() {
    // Generates code to leave a void function
      Emit(PVM.retv);
    } // CodeGen.LeaveVoidFunction

    public static void LeaveFunction(int funcType) {
    // Generates code to leave a function
      if (funcType == Types.voidType) Emit(PVM.retv); else Emit(PVM.ret);
    } // CodeGen.LeaveFunction

    public static void FunctionTrap() {
    // Generates code at end of non-void function to trap failure to return
      Emit(PVM.trap);
    } // CodeGen.FunctionTrap

    public static void BackPatch(int adr) {
    // Stores the current location counter as the address field of the branch or call
    // instruction currently holding a forward reference to adr and repeatedly
    // works through a linked list of such instructions
      while (adr != undefined) {
        int nextAdr = PVM.mem[adr];
        PVM.mem[adr] = codeTop;
        adr = nextAdr;
      }
    } // CodeGen.BackPatch

    public static void Stack() {
    // Generates code to dump the current state of the evaluation stack
      Emit(PVM.stack);
    } // CodeGen.Stack

    public static void Heap() {
    // Generates code to dump the current state of the runtime heap
      Emit(PVM.heap);
    } // CodeGen.Heap

    public static int GetCodeLength() {
    // Returns codeTop = length of generated code
      return codeTop;
    } // CodeGen.GetCodeLength

    public static int GetInitSP() {
    // Returns stkTop = position for initial stack pointer
      return stkTop;
    } // CodeGen.GetInitSP

    public static void OneWord(string mnemonic) {
    // Inline assembly of one word instruction with no operand
      Emit(PVM.OpCode(mnemonic));
    } // CodeGen.OneWord

    public static void TwoWord(string mnemonic, int adr) {
    // Inline assembly of two word instruction with integer operand
      Emit(PVM.OpCode(mnemonic)); Emit(adr);
    } // CodeGen.TwoWord

    public static void Branch(string mnemonic, Label adr) {
    // Inline assembly of two word branch style instruction with Label operand
      Emit(PVM.OpCode(mnemonic)); Emit(adr.Address());
    } // CodeGen.Branch

  } // end CodeGen

} // namespace
