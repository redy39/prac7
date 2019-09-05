// Definition of simple stack machine and simple emulator for Parva compiler (C# version)
// Uses auxiliary methods Push, Pop and Next
// P.D. Terry, Rhodes University, 2015
// As supplied for the last practical of the course
// 2015/10/13

using Library;
using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;

namespace Parva {

  class Processor {
    public int sp;            // Stack pointer
    public int hp;            // Heap pointer
    public int gp;            // Global frame pointer
    public int fp;            // Local frame pointer
    public int mp;            // Mark stack pointer
    public int ir;            // Instruction register
    public int pc;            // Program counter
  } // end Processor

  class PVM {

  // Machine opcodes - not all are used

    public const int
      add     =   1,
      and     =   2,
      anew    =   3,
      bfalse  =   4,
      brn     =   5,
      btrue   =   6,
      bze     =   7,
      call    =   8,
      cap     =   9,
      ceq     =  10,
      cge     =  11,
      cgt     =  12,
      cle     =  13,
      clt     =  14,
      cne     =  15,
      dec     =  16,
      decc    =  17,
      div     =  18,
      dsp     =  19,
      dup     =  20,
      fhdr    =  21,
      halt    =  22,
      heap    =  23,
      i2c     =  24,
      inc     =  25,
      incc    =  26,
      inpb    =  27,
      inpc    =  28,
      inpi    =  29,
      inpl    =  30,
      islet   =  31,
      lda     =  32,
      lda_0   =  33,
      lda_1   =  34,
      lda_2   =  35,
      lda_3   =  36,
      lda_4   =  37,
      lda_5   =  38,
      ldc     =  39,
      ldc_0   =  40,
      ldc_1   =  41,
      ldc_2   =  42,
      ldc_3   =  43,
      ldc_4   =  44,
      ldc_5   =  45,
      ldc_m1  =  46,
      ldga    =  47,
      ldg_0   =  48,
      ldg_1   =  49,
      ldg_2   =  50,
      ldg_3   =  51,
      ldg_4   =  52,
      ldg_5   =  53,
      ldl     =  54,
      ldl_0   =  55,
      ldl_1   =  56,
      ldl_2   =  57,
      ldl_3   =  58,
      ldl_4   =  59,
      ldl_5   =  60,
      ldv     =  61,
      ldxa    =  62,
      low     =  63,
      mul     =  64,
      neg     =  65,
      nop     =  66,
      not     =  67,
      or      =  68,
      pop     =  69,
      prnb    =  70,
      prnc    =  71,
      prni    =  72,
      prnl    =  73,
      prns    =  74,
      rem     =  75,
      ret     =  76,
      retv    =  77,
      stack   =  78,
      stl     =  79,
      stl_0   =  80,
      stl_1   =  81,
      stl_2   =  82,
      stl_3   =  83,
      stl_4   =  84,
      stl_5   =  85,
      stlc    =  86,
      sto     =  87,
      stoc    =  88,
      sub     =  89,
      trap    =  90,

      nul     = 255;                         // leave gap for future

    public static string[] mnemonics = new string[PVM.nul + 1];

  // Memory

    public const int memSize = 8192;         // Limit on memory
    public const int headerSize = 4;
    public static int[] mem;                 // Simulated memory
    static int stackBase, heapBase;          // Limits on cpu.sp

  // Program status

    const int
      running  =  0,
      finished =  1,
      badMem   =  2,
      badData  =  3,
      noData   =  4,
      divZero  =  5,
      badOp    =  6,
      badInd   =  7,
      badVal   =  8,
      badAdr   =  9,
      badAll   = 10,
      nullRef  = 11,
      badFun   = 12;

    static int ps;

  // The processor

    static Processor cpu = new Processor();

  // The timer

    static Stopwatch timer = new Stopwatch();

  // Utilities

    static string padding = "                                                               ";
    const int maxInt = System.Int32.MaxValue;
    const int maxChar = 255;

    static void StackDump(OutFile results, int pcNow) {
    // Dump local variable and stack area - useful for debugging
      int onLine = 0;
      results.Write("\nStack dump at " + pcNow);
      results.Write(" FP:"); results.Write(cpu.fp, 4);
      results.Write(" SP:"); results.WriteLine(cpu.sp, 4);
      for (int i = stackBase - 1; i >= cpu.sp; i--) {
        results.Write(i, 7); results.Write(mem[i], 5);
        onLine++; if (onLine % 8 == 0) results.WriteLine();
      }
      results.WriteLine();
    } // PVM.StackDump

    static void HeapDump(OutFile results, int pcNow) {
    // Dump heap area - useful for debugging
      if (heapBase == cpu.hp)
        results.WriteLine("Empty Heap");
      else {
        int onLine = 0;
        results.Write("\nHeap dump at " + pcNow);
        results.Write(" HP:"); results.Write(cpu.hp, 4);
        results.Write(" HB:"); results.WriteLine(heapBase, 4);
        for (int i = heapBase; i < cpu.hp; i++) {
          results.Write(i, 7); results.Write(mem[i], 5);
          onLine++; if (onLine % 8 == 0) results.WriteLine();
        }
        results.WriteLine();
      }
    } // PVM.HeapDump

    static void Trace(OutFile results, int pcNow, bool traceStack, bool traceHeap) {
    // Simple trace facility for run time debugging
      if (traceStack) StackDump(results, pcNow);
      if (traceHeap)  HeapDump(results, pcNow);
      results.Write(" PC:"); results.Write(pcNow, 5);
      results.Write(" FP:"); results.Write(cpu.fp, 5);
      results.Write(" SP:"); results.Write(cpu.sp, 5);
      results.Write(" HP:"); results.Write(cpu.hp, 5);
      results.Write(" TOS:");
      if (cpu.sp < memSize)
        results.Write(mem[cpu.sp], 5);
      else
        results.Write(" ????");
      results.Write("  " + mnemonics[cpu.ir], -8);
      switch (cpu.ir) {     // two word opcodes
        case PVM.call:
        case PVM.bfalse:
        case PVM.btrue:
        case PVM.brn:
        case PVM.bze:
        case PVM.dsp:
        case PVM.lda:
        case PVM.ldga:
        case PVM.ldc:
        case PVM.ldl:
        case PVM.stl:
        case PVM.stlc:
        case PVM.prns:
          results.Write(mem[cpu.pc], 7); break;
        default: break;
      }
      results.WriteLine();
    } // PVM.Trace

    static void PostMortem(OutFile results, int pcNow) {
    // Reports run time error and position
      results.WriteLine();
      switch (ps) {
        case badMem:   results.Write("Memory violation");         break;
        case badData:  results.Write("Invalid data");             break;
        case noData:   results.Write("No more data");             break;
        case divZero:  results.Write("Division by zero");         break;
        case badOp:    results.Write("Illegal opcode");           break;
        case badInd:   results.Write("Subscript out of range");   break;
        case badVal:   results.Write("Value out of range");       break;
        case badAdr:   results.Write("Bad address");              break;
        case badAll:   results.Write("Heap allocation error");    break;
        case nullRef:  results.Write("Null reference");           break;
        default:       results.Write("Interpreter error!");       break;
      }
      results.WriteLine(" at " + pcNow);
    } // PVM.PostMortem

  // The interpreters and utility methods

    static int Next() {
    // Fetches next word of program and bumps program counter
      return mem[cpu.pc++];
    } // PVM.Next

    static void Push(int value) {
    // Bumps stack pointer and pushes value onto stack
      mem[--cpu.sp] = value;
      if (cpu.sp < cpu.hp) ps = badMem;
    } // PVM.Push

    static int Pop() {
    // Pops and returns top value on stack and bumps stack pointer
      if (cpu.sp == cpu.fp) ps = badMem;
      return mem[cpu.sp++];
    } // PVM.Pop

    static bool InBounds(int p) {
    // Check that memory pointer p does not go out of bounds.  This should not
    // happen with correct code, but it is just as well to check
      if (p == 0) ps = nullRef;
      else if (p < heapBase || p > memSize) ps = badMem;
      return (ps == running);
    } // PVM.InBounds

    public static void Emulator(int initPC, int codeLen, int initSP,
                                InFile data, OutFile results, bool tracing,
                                bool traceStack, bool traceHeap) {
    // Emulates action of the codeLen instructions stored in mem[0 .. codeLen-1], with
    // program counter initialized to initPC, stack pointer initialized to initSP.
    // data and results are used for I/O.  Tracing at the code level may be requested

      int pcNow;                  // current program counter
      int loop;                   // internal loops
      int tos, sos;               // values popped from stack
      int adr;                    // effective address for memory accesses
      int target;                 // destination for branches
      stackBase = initSP;
      heapBase = codeLen;         // initialize boundaries
      cpu.hp = heapBase;          // initialize registers
      cpu.sp = stackBase;
      cpu.gp = stackBase;
      cpu.mp = stackBase;
      cpu.fp = stackBase;
      cpu.pc = initPC;            // initialize program counter
      for (int i = heapBase; i < stackBase; i++)
        mem[i] = 0;               // set entire memory to null or 0

      ps = running;               // prepare to execute
      int ops = 0;
      timer.Start();
      do {
        ops++;
        pcNow = cpu.pc;           // retain for tracing/postmortem
        if (cpu.pc < 0 || cpu.pc >= codeLen) {
          ps = badAdr;
          break;
        }
        cpu.ir = Next();          // fetch
        if (tracing) Trace(results, pcNow, traceStack, traceHeap);
        switch (cpu.ir) {         // execute
          case PVM.nop:           // no operation
            break;
          case PVM.dsp:           // decrement stack pointer (allocate space for variables)
            int localSpace = Next();
            cpu.sp -= localSpace;
            if (InBounds(cpu.sp)) // initialize all local variables to zero/null
              for (loop = 0; loop < localSpace; loop++)
                mem[cpu.sp + loop] = 0;
            break;
          case PVM.ldc:           // push constant value
            Push(Next());
            break;
          case PVM.ldc_m1:        // push constant -1
            Push(-1);
            break;
          case PVM.ldc_0:         // push constant 0
            Push(0);
            break;
          case PVM.ldc_1:         // push constant 1
            Push(1);
            break;
          case PVM.ldc_2:         // push constant 2
            Push(2);
            break;
          case PVM.ldc_3:         // push constant 3
            Push(3);
            break;
          case PVM.ldc_4:         // push constant 4
            Push(4);
            break;
          case PVM.ldc_5:         // push constant 5
            Push(5);
            break;
          case PVM.lda:           // push local address
            adr = cpu.fp - 1 - Next();
            if (InBounds(adr)) Push(adr);
            break;
          case PVM.lda_0:         // push local address 0
            adr = cpu.fp - 1;
            if (InBounds(adr)) Push(adr);
            break;
          case PVM.lda_1:         // push local address 1
            adr = cpu.fp - 2;
            if (InBounds(adr)) Push(adr);
            break;
          case PVM.lda_2:         // push local address 2
            adr = cpu.fp - 3;
            if (InBounds(adr)) Push(adr);
            break;
          case PVM.lda_3:         // push local address 3
            adr = cpu.fp - 4;
            if (InBounds(adr)) Push(adr);
            break;
          case PVM.lda_4:         // push local address 4
            adr = cpu.fp - 5;
            if (InBounds(adr)) Push(adr);
            break;
          case PVM.lda_5:         // push local address 5
            adr = cpu.fp - 6;
            if (InBounds(adr)) Push(adr);
            break;
          case PVM.ldga:        // push global address
            adr = cpu.gp - 1 - Next();
            if (InBounds(adr)) Push(adr);
            break;
          case PVM.ldg_0:         // push local address 0
            adr = cpu.gp - 1;
            if (InBounds(adr)) Push(adr);
            break;
          case PVM.ldg_1:         // push local address 1
            adr = cpu.gp - 2;
            if (InBounds(adr)) Push(adr);
            break;
          case PVM.ldg_2:         // push local address 2
            adr = cpu.gp - 3;
            if (InBounds(adr)) Push(adr);
            break;
          case PVM.ldg_3:         // push local address 3
            adr = cpu.gp - 4;
            if (InBounds(adr)) Push(adr);
            break;
          case PVM.ldg_4:         // push local address 4
            adr = cpu.gp - 5;
            if (InBounds(adr)) Push(adr);
            break;
          case PVM.ldg_5:         // push local address 5
            adr = cpu.gp - 6;
            if (InBounds(adr)) Push(adr);
            break;
          case PVM.ldl:           // push local value
            adr = cpu.fp - 1 - Next();
            if (InBounds(adr)) Push(mem[adr]);
            break;
          case PVM.ldl_0:         // push value of local variable 0
            adr = cpu.fp - 1;
            if (InBounds(adr)) Push(mem[adr]);
            break;
          case PVM.ldl_1:         // push value of local variable 1
            adr = cpu.fp - 2;
            if (InBounds(adr)) Push(mem[adr]);
            break;
          case PVM.ldl_2:         // push value of local variable 2
            adr = cpu.fp - 3;
            if (InBounds(adr)) Push(mem[adr]);
            break;
          case PVM.ldl_3:         // push value of local variable 3
            adr = cpu.fp - 4;
            if (InBounds(adr)) Push(mem[adr]);
            break;
          case PVM.ldl_4:         // push value of local variable 4
            adr = cpu.fp - 5;
            if (InBounds(adr)) Push(mem[adr]);
            break;
          case PVM.ldl_5:         // push value of local variable 5
            adr = cpu.fp - 6;
            if (InBounds(adr)) Push(mem[adr]);
            break;
          case PVM.stl:           // store local value
            adr = cpu.fp - 1 - Next();
            if (InBounds(adr)) mem[adr] = Pop();
            break;
          case PVM.stlc:          // character checked pop to local variable
            tos = Pop(); adr = cpu.fp - 1 - Next();
            if (InBounds(adr))
              if (tos >= 0 && tos <= maxChar) mem[adr] = tos;
              else ps = badVal;
            break;
          case PVM.stl_0:         // pop to local variable 0
            adr = cpu.fp - 1;
            if (InBounds(adr)) mem[adr] = Pop();
            break;
          case PVM.stl_1:         // pop to local variable 1
            adr = cpu.fp - 2;
            if (InBounds(adr)) mem[adr] = Pop();
            break;
          case PVM.stl_2:         // pop to local variable 2
            adr = cpu.fp - 3;
            if (InBounds(adr)) mem[adr] = Pop();
            break;
          case PVM.stl_3:         // pop to local variable 3
            adr = cpu.fp - 4;
            if (InBounds(adr)) mem[adr] = Pop();
            break;
          case PVM.stl_4:         // pop to local variable 4
            adr = cpu.fp - 5;
            if (InBounds(adr)) mem[adr] = Pop();
            break;
          case PVM.stl_5:         // pop to local variable 5
            adr = cpu.fp - 6;
            if (InBounds(adr)) mem[adr] = Pop();
            break;
          case PVM.ldv:           // dereference
            adr = Pop();
            if (InBounds(adr)) Push(mem[adr]);
            break;
          case PVM.sto:           // store
            tos = Pop(); adr = Pop();
            if (InBounds(adr)) mem[adr] = tos;
            break;
          case PVM.stoc:          // character checked store
            tos = Pop(); adr = Pop();
            if (InBounds(adr))
              if (tos >= 0 && tos <= maxChar) mem[adr] = tos;
              else ps = badVal;
            break;
          case PVM.ldxa:          // heap array indexing
            adr = Pop();
            int heapPtr = Pop();
            if (heapPtr == 0) ps = nullRef;
            else if (heapPtr < heapBase || heapPtr >= cpu.hp) ps = badMem;
            else if (adr < 0 || adr >= mem[heapPtr]) ps = badInd;
            else Push(heapPtr + adr + 1);
            break;
          case PVM.inpi:          // integer input
            adr = Pop();
            if (InBounds(adr)) {
              mem[adr] = data.ReadInt();
              if (data.Error()) ps = badData;
            }
            break;
          case PVM.inpb:          // boolean input
            adr = Pop();
            if (InBounds(adr)) {
              mem[adr] = data.ReadBool() ? 1 : 0;
              if (data.Error()) ps = badData;
            }
            break;
          case PVM.inpc:          // character input
            adr = Pop();
            if (InBounds(adr)) {
              mem[adr] = data.ReadChar();
              if (data.Error()) ps = badData;
            }
            break;
          case PVM.inpl:          // skip to end of input line
            data.ReadLine();
            break;
          case PVM.i2c:           // check convert character to integer
            if (mem[cpu.sp] < 0 || mem[cpu.sp] > maxChar) ps = badVal;
            break;
          case PVM.prni:          // integer output
            if (tracing) results.Write(padding);
            results.Write(Pop(), 0);
            if (tracing) results.WriteLine();
            break;
          case PVM.prnb:          // boolean output
            if (tracing) results.Write(padding);
            if (Pop() != 0) results.Write(" true  "); else results.Write(" false ");
            if (tracing) results.WriteLine();
            break;
          case PVM.prnc:          // character output
            if (tracing) results.Write(padding);
            results.Write((char) (Math.Abs(Pop()) % (maxChar + 1)), 1);
            if (tracing) results.WriteLine();
            break;
          case PVM.prns:          // string output
            if (tracing) results.Write(padding);
            loop = Next();
            while (ps == running && mem[loop] != 0) {
              results.Write((char) mem[loop]); loop--;
              if (loop < stackBase) ps = badMem;
            }
            if (tracing) results.WriteLine();
            break;
          case PVM.prnl:          // newline
            results.WriteLine();
            break;
          case PVM.neg:           // integer negation
            Push(-Pop());
            break;
          case PVM.add:           // integer addition
            tos = Pop(); Push(Pop() + tos);
            break;
          case PVM.sub:           // integer subtraction
            tos = Pop(); Push(Pop() - tos);
            break;
          case PVM.mul:           // integer multiplication
            tos = Pop();
            sos = Pop();
            if (tos != 0 && Math.Abs(sos) > maxInt / Math.Abs(tos)) ps = badVal;
            else Push(sos * tos);
            break;
          case PVM.div:           // integer division (quotient)
            tos = Pop();
            if (tos == 0) ps = divZero;
            else Push(Pop() / tos);
            break;
          case PVM.rem:           // integer division (remainder)
            tos = Pop();
            if (tos == 0) ps = divZero;
            else Push(Pop() % tos);
            break;
          case PVM.not:           // logical negation
            Push(Pop() == 0 ? 1 : 0);
            break;
          case PVM.and:           // logical and
            tos = Pop(); Push(Pop() & tos);
            break;
          case PVM.or:            // logical or
            tos = Pop(); Push(Pop() | tos);
            break;
          case PVM.ceq:           // logical equality
            tos = Pop(); Push(Pop() == tos ? 1 : 0);
            break;
          case PVM.cne:           // logical inequality
            tos = Pop(); Push(Pop() != tos ? 1 : 0);
            break;
          case PVM.clt:           // logical less
            tos = Pop(); Push(Pop() <  tos ? 1 : 0);
            break;
          case PVM.cle:           // logical less or equal
            tos = Pop(); Push(Pop() <= tos ? 1 : 0);
            break;
          case PVM.cgt:           // logical greater
            tos = Pop(); Push(Pop() >  tos ? 1 : 0);
            break;
          case PVM.cge:           // logical greater or equal
            tos = Pop(); Push(Pop() >= tos ? 1 : 0);
            break;
          case PVM.brn:           // unconditional branch
            cpu.pc = Next();
            if (cpu.pc < 0 || cpu.pc >= codeLen) ps = badAdr;
            break;
          case PVM.bze:           // pop top of stack, branch if false
            target = Next();
            if (Pop() == 0) {
              cpu.pc = target;
              if (cpu.pc < 0 || cpu.pc >= codeLen) ps = badAdr;
            }
            break;
          case PVM.bfalse:        // conditional short circuit "and" branch
            target = Next();
            if (mem[cpu.sp] == 0) cpu.pc = target; else cpu.sp++;
            break;
          case PVM.btrue:         // conditional short circuit "or" branch
            target = Next();
            if (mem[cpu.sp] == 1) cpu.pc = target; else cpu.sp++;
            break;
          case PVM.anew:          // heap array allocation
            int size = Pop();
            if (size <= 0 || size + 1 > cpu.sp - cpu.hp - 2)
              ps = badAll;
            else {
              mem[cpu.hp] = size; // first element stores size for bounds checking
              Push(cpu.hp);
              cpu.hp += size + 1; // bump heap pointer
                                  // elements are already initialized to 0 / null (why?)
            }
            break;
          case PVM.halt:          // halt
            ps = finished;
            break;
          case PVM.inc:           // integer ++
            adr = Pop();
            if (InBounds(adr)) mem[adr]++;
            break;
          case PVM.dec:           // integer --
            adr = Pop();
            if (InBounds(adr)) mem[adr]--;
            break;
          case PVM.incc:          // ++ characters
            adr = Pop();
            if (InBounds(adr))
              if (mem[adr] < maxChar) mem[adr]++;
              else ps = badVal;
            break;
          case PVM.decc:          // -- characters
            adr = Pop();
              if (mem[adr] > 0) mem[adr]--;
              else ps = badVal;
            break;
          case PVM.stack:         // stack dump (debugging)
            StackDump(results, pcNow);
            break;
          case PVM.heap:          // heap dump (debugging)
            HeapDump(results, pcNow);
            break;
          case PVM.fhdr:          // allocate frame header
            if (InBounds(cpu.sp - headerSize)) {
              mem[cpu.sp - headerSize] = cpu.mp;
              cpu.mp = cpu.sp;
              cpu.sp -= headerSize;
            }
            break;
          case PVM.call:          // call function
            if (mem[cpu.pc] < 0) ps = badAdr;
            else {
              mem[cpu.mp - 2] = cpu.fp;
              mem[cpu.mp - 3] = cpu.pc + 1;
              cpu.fp = cpu.mp;
              cpu.pc = mem[cpu.pc];
            }
            break;
          case PVM.retv:          // return from void function
            cpu.sp = cpu.fp;
            cpu.mp = mem[cpu.fp - 4];
            cpu.pc = mem[cpu.fp - 3];
            cpu.fp = mem[cpu.fp - 2];
            break;
          case PVM.ret:           // return from non-void function
            cpu.sp = cpu.fp - 1;
            cpu.mp = mem[cpu.fp - 4];
            cpu.pc = mem[cpu.fp - 3];
            cpu.fp = mem[cpu.fp - 2];
            break;
          case PVM.trap:          // trap falling off end of function
            ps = badFun;
            break;
          case PVM.low:           // toLowerCase
            Push(Char.ToLower((char) Pop()));
            break;
          case PVM.cap:           // toUpperCase
            Push(Char.ToUpper((char) Pop()));
            break;
          case PVM.islet:         // isLetter
            tos = Pop();
            Push(Char.IsLetter((char) tos) ? 1 : 0);
            break;
          case PVM.pop:           // pop and discard tos
            tos = Pop();
            break;
          case PVM.dup:           // duplicate tos
            tos = Pop();
            Push(tos); Push(tos);
            break;
          default:                // unrecognized opcode
            ps = badOp;
            break;
        }
      } while (ps == running);
      TimeSpan ts = timer.Elapsed;
      // Format and display the TimeSpan value.
      string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                                         ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
      Console.WriteLine("\n\n" + ops + " operations.  Run Time " + elapsedTime + "\n\n");
      if (ps != finished) PostMortem(results, pcNow);
      timer.Reset();
      timer.Stop();

    } // PVM.Emulator

    public static void QuickInterpret(int codeLen, int initSP) {
    // Interprets the codeLen instructions stored in mem, with stack pointer
    // initialized to initSP.  Use StdIn and StdOut without asking
      Console.WriteLine("\nHit <Enter> to start");
      Console.ReadLine();
      bool tracing = false;
      InFile data = new InFile("");
      OutFile results = new OutFile("");
      Emulator(0, codeLen, initSP, data, results, false, false, false);
    } // PVM.QuickInterpret

    public static void Interpret(int codeLen, int initSP) {
    // Interactively opens data and results files.  Then interprets the codeLen
    // instructions stored in mem, with stack pointer initialized to initSP
      Console.Write("\nTrace execution (y/N/q)? ");
      char reply = (Console.ReadLine() + " ").ToUpper()[0];
      bool traceStack = false, traceHeap = false;
      if (reply != 'Q') {
        bool tracing = reply == 'Y';
        if (tracing) {
          Console.Write("\nTrace Stack (y/N)? ");
          traceStack = (Console.ReadLine() + " ").ToUpper()[0] == 'Y';
          Console.Write("\nTrace Heap (y/N)? ");
          traceHeap = (Console.ReadLine() + " ").ToUpper()[0] == 'Y';
        }
        Console.Write("\nData file [STDIN] ? ");
        InFile data = new InFile(Console.ReadLine());
        Console.Write("\nResults file [STDOUT] ? ");
        OutFile results = new OutFile(Console.ReadLine());
        Emulator(0, codeLen, initSP, data, results, tracing, traceStack, traceHeap);
        results.Close();
        data.Close();
      }
    } // PVM.Interpret

    public static void ListCode(string fileName, int codeLen) {
    // Lists the codeLen instructions stored in mem on a named output file
      int i, j;

      if (fileName == null) return;
      OutFile codeFile = new OutFile(fileName);

      /* ------------- The following may be useful for debugging the interpreter
      i = 0;
      while (i < codeLen) {
        codeFile.Write(mem[i], 5);
        if ((i + 1) % 15 == 0) codeFile.WriteLine();
        i++;
      }
      codeFile.WriteLine();

      ------------- */

      i = 0;
      codeFile.WriteLine("ASSEM\nBEGIN");
      while (i < codeLen && mem[i] != PVM.nul) {
        int o = mem[i] % (PVM.nul + 1); // force in range
        codeFile.Write("  {");
        codeFile.Write(i, 5);
        codeFile.Write(" } ");
        codeFile.Write(mnemonics[o], -8);
        switch (o) {                    // two word opcodes
          case PVM.call:
          case PVM.bfalse:
          case PVM.btrue:
          case PVM.brn:
          case PVM.bze:
          case PVM.dsp:
          case PVM.lda:
          case PVM.ldga:
          case PVM.ldc:
          case PVM.ldl:
          case PVM.stl:
          case PVM.stlc:
            i = (i + 1) % memSize; codeFile.Write(mem[i]);
            break;

          case PVM.prns:                // special case
            i = (i + 1) % memSize;
            j = mem[i]; codeFile.Write(" \"");
            while (mem[j] != 0) {
              switch (mem[j]) {
                case '\\' : codeFile.Write("\\\\"); break;
                case '\"' : codeFile.Write("\\\""); break;
                case '\'' : codeFile.Write("\\\'"); break;
                case '\b' : codeFile.Write("\\b");  break;
                case '\t' : codeFile.Write("\\t");  break;
                case '\n' : codeFile.Write("\\n");  break;
                case '\f' : codeFile.Write("\\f");  break;
                case '\r' : codeFile.Write("\\r");  break;
                default   : codeFile.Write((char) mem[j]); break;
              }
              j--;
            }
            codeFile.Write("\"");
            break;

        } // switch
        i = (i + 1) % memSize;
        codeFile.WriteLine();
      } // while (i < codeLen)
      codeFile.WriteLine("END.");
      codeFile.Close();
    } // PVM.ListCode

    public static int OpCode(string str) {
    // Maps str to opcode, or to PVM.nul if no match can be found
    // Simple linear search.  A hashtable or dictionary might be a useful improvement!
      int op = 0;
      while (op != PVM.nul && !(str.ToUpper().Equals(mnemonics[op]))) op++;
      return op;
    } // PVM.OpCode

    public static void Init() {
    // Initializes stack machine
      mem = new int [memSize + 1];                    // virtual machine memory
      for (int i = 0; i <= memSize; i++) mem[i] = 0;  // set entire memory to null or 0

      // Initialize mnemonic table this way for ease of modification in exercises
      // A hashtable or dictionary might be a useful improvement!
      for (int i = 0; i <= PVM.nul; i++) mnemonics[i] = "";

      mnemonics[PVM.add]      = "ADD";
      mnemonics[PVM.and]      = "AND";
      mnemonics[PVM.anew]     = "ANEW";
      mnemonics[PVM.bfalse]   = "BFALSE";
      mnemonics[PVM.brn]      = "BRN";
      mnemonics[PVM.btrue]    = "BTRUE";
      mnemonics[PVM.bze]      = "BZE";
      mnemonics[PVM.call]     = "CALL";
      mnemonics[PVM.cap]      = "CAP";
      mnemonics[PVM.ceq]      = "CEQ";
      mnemonics[PVM.cge]      = "CGE";
      mnemonics[PVM.cgt]      = "CGT";
      mnemonics[PVM.cle]      = "CLE";
      mnemonics[PVM.clt]      = "CLT";
      mnemonics[PVM.cne]      = "CNE";
      mnemonics[PVM.dec]      = "DEC";
      mnemonics[PVM.decc]     = "DECC";
      mnemonics[PVM.div]      = "DIV";
      mnemonics[PVM.dsp]      = "DSP";
      mnemonics[PVM.dup]      = "DUP";
      mnemonics[PVM.fhdr]     = "FHDR";
      mnemonics[PVM.halt]     = "HALT";
      mnemonics[PVM.heap]     = "HEAP";
      mnemonics[PVM.i2c]      = "I2C";
      mnemonics[PVM.inc]      = "INC";
      mnemonics[PVM.incc]     = "INCC";
      mnemonics[PVM.inpb]     = "INPB";
      mnemonics[PVM.inpc]     = "INPC";
      mnemonics[PVM.inpi]     = "INPI";
      mnemonics[PVM.islet]    = "ISLET";
      mnemonics[PVM.lda]      = "LDA";
      mnemonics[PVM.lda_0]    = "LDA_0";
      mnemonics[PVM.lda_1]    = "LDA_1";
      mnemonics[PVM.lda_2]    = "LDA_2";
      mnemonics[PVM.lda_3]    = "LDA_3";
      mnemonics[PVM.lda_4]    = "LDA_4";
      mnemonics[PVM.lda_5]    = "LDA_5";
      mnemonics[PVM.ldc]      = "LDC";
      mnemonics[PVM.ldc_0]    = "LDC_0";
      mnemonics[PVM.ldc_1]    = "LDC_1";
      mnemonics[PVM.ldc_2]    = "LDC_2";
      mnemonics[PVM.ldc_3]    = "LDC_3";
      mnemonics[PVM.ldc_4]    = "LDC_4";
      mnemonics[PVM.ldc_5]    = "LDC_5";
      mnemonics[PVM.ldc_m1]   = "LDC_M1";
      mnemonics[PVM.ldga]     = "LDGA";
      mnemonics[PVM.ldg_0]    = "LDG_0";
      mnemonics[PVM.ldg_1]    = "LDG_1";
      mnemonics[PVM.ldg_2]    = "LDG_2";
      mnemonics[PVM.ldg_3]    = "LDG_3";
      mnemonics[PVM.ldg_4]    = "LDG_4";
      mnemonics[PVM.ldg_5]    = "LDG_5";
      mnemonics[PVM.ldl]      = "LDL";
      mnemonics[PVM.ldl_0]    = "LDL_0";
      mnemonics[PVM.ldl_1]    = "LDL_1";
      mnemonics[PVM.ldl_2]    = "LDL_2";
      mnemonics[PVM.ldl_3]    = "LDL_3";
      mnemonics[PVM.ldl_4]    = "LDL_4";
      mnemonics[PVM.ldl_5]    = "LDL_5";
      mnemonics[PVM.ldv]      = "LDV";
      mnemonics[PVM.ldxa]     = "LDXA";
      mnemonics[PVM.low]      = "LOW";
      mnemonics[PVM.mul]      = "MUL";
      mnemonics[PVM.neg]      = "NEG";
      mnemonics[PVM.nop]      = "NOP";
      mnemonics[PVM.not]      = "NOT";
      mnemonics[PVM.nul]      = "NUL";
      mnemonics[PVM.or]       = "OR";
      mnemonics[PVM.pop]      = "POP";
      mnemonics[PVM.prnb]     = "PRNB";
      mnemonics[PVM.prnc]     = "PRNC";
      mnemonics[PVM.prni]     = "PRNI";
      mnemonics[PVM.prnl]     = "PRNL";
      mnemonics[PVM.prns]     = "PRNS";
      mnemonics[PVM.rem]      = "REM";
      mnemonics[PVM.ret]      = "RET";
      mnemonics[PVM.retv]     = "RETV";
      mnemonics[PVM.stack]    = "STACK";
      mnemonics[PVM.stl]      = "STL";
      mnemonics[PVM.stlc]     = "STLC";
      mnemonics[PVM.stl_0]    = "STL_0";
      mnemonics[PVM.stl_1]    = "STL_1";
      mnemonics[PVM.stl_2]    = "STL_2";
      mnemonics[PVM.stl_3]    = "STL_3";
      mnemonics[PVM.stl_4]    = "STL_4";
      mnemonics[PVM.stl_5]    = "STL_5";
      mnemonics[PVM.sto]      = "STO";
      mnemonics[PVM.stoc]     = "STOC";
      mnemonics[PVM.sub]      = "SUB";
      mnemonics[PVM.trap]     = "TRAP";

    } // PVM.Init

  } // end PVM

} // end namespace
